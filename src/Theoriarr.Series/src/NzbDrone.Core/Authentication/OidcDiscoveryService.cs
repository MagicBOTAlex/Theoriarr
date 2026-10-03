using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Authentication
{
    public interface IOidcDiscoveryService
    {
        bool IsDiscoverable(string authority);
    }

    public class OidcDiscoveryService : IOidcDiscoveryService
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public OidcDiscoveryService(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public bool IsDiscoverable(string authority)
        {
            if (authority.IsNullOrWhiteSpace())
            {
                return false;
            }

            // The authority is admin-supplied, so never let it point the server at itself or
            // at another internal host (SSRF). Only public HTTP(S) endpoints are fetched.
            if (IsBlockedAuthority(authority, out var reason))
            {
                _logger.Warn("Refusing to query the OIDC authority '{0}': {1}", authority, reason);

                return false;
            }

            try
            {
                var request = new HttpRequestBuilder(authority)
                    .Resource(".well-known/openid-configuration")
                    .Build();

                request.RequestTimeout = RequestTimeout;
                request.SuppressHttpError = true;

                var response = _httpClient.Get<OidcDiscoveryDocument>(request);

                if (response.HasHttpError)
                {
                    _logger.Debug("OIDC configuration request for {0} failed with status {1}", request.Url, response.StatusCode);

                    return false;
                }

                var document = response.Resource;

                if (document == null)
                {
                    _logger.Debug("OIDC configuration request for {0} returned an invalid response", request.Url);

                    return false;
                }

                if (document.AuthorizationEndpoint.IsNullOrWhiteSpace() || document.TokenEndpoint.IsNullOrWhiteSpace())
                {
                    _logger.Debug("OIDC configuration from {0} is missing the authorization or token endpoint", request.Url);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to retrieve the OIDC configuration from {0}", authority);

                return false;
            }
        }

        private static bool IsBlockedAuthority(string authority, out string reason)
        {
            reason = null;

            if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
            {
                reason = "not an absolute URI";

                return true;
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                reason = $"unsupported scheme '{uri.Scheme}'";

                return true;
            }

            var host = uri.DnsSafeHost.Trim('[', ']');

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                reason = "local host name";

                return true;
            }

            if (IPAddress.TryParse(host, out var literal))
            {
                if (IsBlockedAddress(literal))
                {
                    reason = "non-public address";

                    return true;
                }

                return false;
            }

            try
            {
                foreach (var address in Dns.GetHostAddresses(host))
                {
                    if (IsBlockedAddress(address))
                    {
                        reason = "resolves to a non-public address";

                        return true;
                    }
                }
            }
            catch (SocketException)
            {
                // An unresolvable name cannot be reached; let the normal request path report
                // the failure instead of treating transient DNS trouble as an SSRF block.
            }

            return false;
        }

        private static bool IsBlockedAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();

            if (bytes.Length == 4)
            {
                // 0.0.0.0/8, 10/8, 100.64/10 (CGNAT), 127/8, 169.254/16 (link-local),
                // 172.16/12, 192.168/16, 198.18/15 (benchmarking) and multicast/reserved.
                return bytes[0] == 0 ||
                       bytes[0] == 10 ||
                       (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                       (bytes[0] == 169 && bytes[1] == 254) ||
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                       (bytes[0] == 192 && bytes[1] == 168) ||
                       (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                       (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) ||
                       bytes[0] >= 224;
            }

            if (bytes.Length == 16)
            {
                // ::1/:: unspecified, fc00::/7 (unique local), fe80::/10 (link-local),
                // fec0::/10 (site-local) and ff00::/8 (multicast).
                if (address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6Any))
                {
                    return true;
                }

                if ((bytes[0] & 0xfe) == 0xfc)
                {
                    return true;
                }

                if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                {
                    return true;
                }

                if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0)
                {
                    return true;
                }

                if (bytes[0] == 0xff)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class OidcDiscoveryDocument
    {
        [JsonProperty("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; }

        [JsonProperty("token_endpoint")]
        public string TokenEndpoint { get; set; }
    }
}
