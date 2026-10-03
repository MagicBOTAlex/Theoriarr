using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace Sonarr.Http.Middleware
{
    // Replaces ASP.NET Core's HostFiltering middleware so Allowed Hosts can also contain
    // CIDR networks (which HostFilteringOptions does not support). Exact hosts, leading
    // wildcards (*.example.com), the "*" wildcard and the always-allowed loopback/hostname
    // values keep the upstream behaviour; a request whose Host header is an IP inside a
    // configured network is also accepted.
    public class AllowedHostsMiddleware
    {
        private static readonly Logger _logger = LogManager.GetLogger("HostFiltering");

        // Always allow localhost, loopback addresses and the hostname of the server, even
        // if they aren't configured in the allowed hosts list, so users can access the UI.
        private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1", "[::1]" };

        private readonly RequestDelegate _next;
        private readonly List<string> _allowedHosts;
        private readonly List<AllowedHostsNetwork> _allowedNetworks;
        private readonly bool _allowAll;

        public AllowedHostsMiddleware(RequestDelegate next, IConfigFileProvider configFileProvider)
        {
            _next = next;

            var configured = AllowedHostsParser.Parse(configFileProvider.AllowedHosts);

            if (!configured.Any())
            {
                _allowAll = true;
                _allowedHosts = new List<string>();
                _allowedNetworks = new List<AllowedHostsNetwork>();

                _logger.Info("Allowed Hosts is not configured, accepting requests for any host");

                return;
            }

            _allowedHosts = AllowedHostsParser.ParseHosts(configFileProvider.AllowedHosts)
                .Concat(LoopbackHosts)
                .Concat(GetServerHostNames())
                .Distinct()
                .ToList();

            _allowedNetworks = AllowedHostsParser.ParseNetworks(configFileProvider.AllowedHosts);

            foreach (var network in _allowedNetworks)
            {
                _logger.Info("Accepting requests for hosts in network {0}/{1}", network.Network, network.PrefixLength);
            }

            _logger.Info("Accepting requests for hosts: {0}", string.Join(", ", _allowedHosts));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsAllowed(context.Request.Host.Host))
            {
                await _next(context);

                return;
            }

            _logger.Warn("The host '{0}' does not match an allowed host.", context.Request.Host.Host);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await context.Response.WriteAsync("Bad Request: Invalid Host header");
        }

        private bool IsAllowed(string host)
        {
            if (_allowAll)
            {
                return true;
            }

            if (host.IsNullOrWhiteSpace())
            {
                return true;
            }

            foreach (var allowedHost in _allowedHosts)
            {
                if (allowedHost == "*")
                {
                    return true;
                }

                if (string.Equals(allowedHost, host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (allowedHost.Length > 1 &&
                    allowedHost[0] == '*' &&
                    host.Length > allowedHost.Length - 1 &&
                    host.EndsWith(allowedHost.Substring(1), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (_allowedNetworks.Count > 0 && TryParseHostAddress(host, out var address))
            {
                return _allowedNetworks.Any(network => network.Contains(address));
            }

            return false;
        }

        private static bool TryParseHostAddress(string host, out IPAddress address)
        {
            var value = host;

            if (value.Length > 1 && value[0] == '[' && value[value.Length - 1] == ']')
            {
                value = value.Substring(1, value.Length - 2);
            }

            return IPAddress.TryParse(value, out address) &&
                   address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
        }

        private static List<string> GetServerHostNames()
        {
            var hostNames = new List<string>();

            if (Environment.MachineName.IsNotNullOrWhiteSpace())
            {
                hostNames.Add(Environment.MachineName);
            }

            try
            {
                var hostName = Dns.GetHostName();

                if (hostName.IsNotNullOrWhiteSpace())
                {
                    hostNames.Add(hostName);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to get the hostname of the server");
            }

            return hostNames;
        }
    }
}
