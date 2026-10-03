using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Configuration
{
    public static class AllowedHostsParser
    {
        private static readonly char[] Separators = { ',', ';' };

        public static List<string> Parse(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return new List<string>();
            }

            return value.Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(Normalize)
                        .Distinct()
                        .ToList();
        }

        // Literal hosts/wildcards only. CIDR entries are handled separately by ParseNetworks
        // so host filtering can match them against the request IP rather than the Host header.
        public static List<string> ParseHosts(string value)
        {
            return Parse(value).Where(entry => !TryParseNetwork(entry, out _, out _)).ToList();
        }

        public static List<AllowedHostsNetwork> ParseNetworks(string value)
        {
            var networks = new List<AllowedHostsNetwork>();

            if (value.IsNullOrWhiteSpace())
            {
                return networks;
            }

            foreach (var entry in value.Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseNetwork(entry, out var address, out var prefixLength))
                {
                    networks.Add(new AllowedHostsNetwork(address, prefixLength));
                }
            }

            return networks;
        }

        public static bool IsValidHost(string host)
        {
            if (host.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (host.StartsWith("*."))
            {
                return Uri.CheckHostName(host.Substring(2)) == UriHostNameType.Dns;
            }

            return Uri.CheckHostName(host) != UriHostNameType.Unknown;
        }

        public static bool IsValidHostOrNetwork(string host)
        {
            return IsValidHost(host) || TryParseNetwork(host, out _, out _);
        }

        // Lenient CIDR parse for Allowed Hosts: host bits are masked off, so
        // "192.168.50.1/24" is accepted and normalised to 192.168.50.0/24.
        public static bool TryParseNetwork(string value, out IPAddress network, out int prefixLength)
        {
            network = null;
            prefixLength = 0;

            if (value.IsNullOrWhiteSpace())
            {
                return false;
            }

            var parts = value.Split('/', StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                return false;
            }

            var addressPart = parts[0];

            if (!IPAddress.TryParse(addressPart, out var parsedAddress))
            {
                return false;
            }

            if (parsedAddress.AddressFamily == AddressFamily.InterNetwork && addressPart.Count(c => c == '.') != 3)
            {
                return false;
            }

            var maxPrefixLength = parsedAddress.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;

            if (!int.TryParse(parts[1], out var parsedPrefixLength) ||
                parsedPrefixLength < 0 ||
                parsedPrefixLength > maxPrefixLength)
            {
                return false;
            }

            network = MaskToNetwork(parsedAddress, parsedPrefixLength);
            prefixLength = parsedPrefixLength;

            return true;
        }

        private static IPAddress MaskToNetwork(IPAddress address, int prefixLength)
        {
            var bytes = address.GetAddressBytes();

            for (var i = 0; i < bytes.Length; i++)
            {
                var prefixBitsInByte = prefixLength - (i * 8);

                if (prefixBitsInByte >= 8)
                {
                    continue;
                }

                bytes[i] = prefixBitsInByte <= 0 ? (byte)0 : (byte)(bytes[i] & (0xFF << (8 - prefixBitsInByte)));
            }

            return new IPAddress(bytes);
        }

        private static string Normalize(string host)
        {
            return host.IsValidIpAddress() ? host.ToUrlHost() : host;
        }
    }

    // A parsed CIDR range from the Allowed Hosts list. Kept in Core so both the
    // settings validation and the host filtering middleware share one parser.
    public readonly struct AllowedHostsNetwork
    {
        public AllowedHostsNetwork(IPAddress network, int prefixLength)
        {
            Network = network;
            PrefixLength = prefixLength;
        }

        public IPAddress Network { get; }

        public int PrefixLength { get; }

        public bool Contains(IPAddress address)
        {
            var network = Network;
            var candidate = address;

            if (network.IsIPv4MappedToIPv6)
            {
                network = network.MapToIPv4();
            }

            if (candidate.IsIPv4MappedToIPv6)
            {
                candidate = candidate.MapToIPv4();
            }

            if (network.AddressFamily != candidate.AddressFamily)
            {
                return false;
            }

            var networkBytes = network.GetAddressBytes();
            var candidateBytes = candidate.GetAddressBytes();
            var fullBytes = PrefixLength / 8;
            var remainingBits = PrefixLength % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != candidateBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));

                if ((networkBytes[fullBytes] & mask) != (candidateBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
