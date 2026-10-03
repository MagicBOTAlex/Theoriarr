using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Options;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.MetadataSource
{
    /// <summary>Describes the effective metadata (Providarr) endpoint and any warning about it.</summary>
    public class MetadataProviderStatus
    {
        public bool IsSharedPublic { get; set; }
        public string BaseUrl { get; set; }
        public string Warning { get; set; }
    }

    public interface IMetadataProviderStatusService
    {
        MetadataProviderStatus GetStatus();
    }

    /// <summary>
    /// Resolves the effective Providarr endpoint and, when the shared public instance is in
    /// use, builds the warning about its aggressive cache TTLs / inbound rate limit. Shared by
    /// the health check and the Metadata Source settings page.
    /// </summary>
    public class MetadataProviderStatusService : IMetadataProviderStatusService
    {
        private const string PolicyPath = "/v1/policy";

        private static readonly TimeSpan PolicyRequestTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PolicyCacheDuration = TimeSpan.FromMinutes(5);

        private readonly IHttpClient _client;
        private readonly MetadataOptions _options;
        private readonly Logger _logger;

        private readonly object _policyLock = new();

        private string _cachedWarningKey;
        private string _cachedWarning;
        private DateTime _cachedWarningAt = DateTime.MinValue;

        public MetadataProviderStatusService(IHttpClient client, IOptions<MetadataOptions> options, Logger logger)
        {
            _client = client;
            _options = options?.Value ?? new MetadataOptions();
            _logger = logger;
        }

        public MetadataProviderStatus GetStatus()
        {
            var baseUrl = _options.ResolveBaseUrl();
            var seriesUrl = _options.ResolveSeriesUrl();
            var movieUrl = _options.ResolveMovieUrl();

            // The shared-public warning must reflect the endpoints actually used, so a
            // MovieUrl/SeriesUrl override away from the default host suppresses it.
            var seriesShared = MetadataOptions.TryGetHost(seriesUrl, out var seriesHost)
                && string.Equals(seriesHost, MetadataOptions.ForcedRetryHost, StringComparison.OrdinalIgnoreCase);
            var movieShared = MetadataOptions.TryGetHost(movieUrl, out var movieHost)
                && string.Equals(movieHost, MetadataOptions.ForcedRetryHost, StringComparison.OrdinalIgnoreCase);
            var isSharedPublic = seriesShared || movieShared;

            var effectiveUrl = seriesUrl;
            var resolvedUrl = MetadataOptions.TryGetOrigin(effectiveUrl, out var origin) ? origin : baseUrl;

            var status = new MetadataProviderStatus
            {
                BaseUrl = resolvedUrl,
                IsSharedPublic = isSharedPublic
            };

            if (!isSharedPublic)
            {
                return status;
            }

            status.Warning = GetSharedPublicWarning(baseUrl, effectiveUrl);

            return status;
        }

        private string GetSharedPublicWarning(string baseUrl, string effectiveUrl)
        {
            var cacheKey = $"{baseUrl}|{effectiveUrl}";

            lock (_policyLock)
            {
                if (_cachedWarningKey == cacheKey
                    && DateTime.UtcNow - _cachedWarningAt < PolicyCacheDuration)
                {
                    return _cachedWarning;
                }
            }

            var warning = BuildSharedPublicWarning(baseUrl);

            lock (_policyLock)
            {
                _cachedWarningKey = cacheKey;
                _cachedWarning = warning;
                _cachedWarningAt = DateTime.UtcNow;
            }

            return warning;
        }

        private string BuildSharedPublicWarning(string baseUrl)
        {
            try
            {
                var request = new HttpRequest($"{baseUrl}{PolicyPath}")
                {
                    RequestTimeout = PolicyRequestTimeout
                };

                var response = _client.Get(request);
                var policy = Json.Deserialize<ProvidarrPolicy>(response.Content);

                var defaultTtl = FormatDuration(policy?.Cache?.DefaultTtlSeconds);
                var metadataTtl = FormatDuration(policy?.Cache?.MetadataTtlSeconds());
                var inbound = policy?.RateLimits?.Inbound;

                _logger.Debug("Providarr policy from {0}: default TTL {1}s, metadata TTL {2}s, inbound {3}/s (burst {4})",
                    baseUrl,
                    policy?.Cache?.DefaultTtlSeconds,
                    policy?.Cache?.MetadataTtlSeconds(),
                    inbound?.RequestsPerSecond,
                    inbound?.Burst);

                return
                    "You are using the shared public Providarr instance (providarr.deprived.dev). "
                    + $"It caches aggressively: default TTL {defaultTtl}, movie/series metadata TTL {metadataTtl}. "
                    + $"Inbound rate limit: {inbound?.RequestsPerSecond:0.##}/s (burst {inbound?.Burst}). "
                    + "Host your own Providarr instance and point Theoriarr at it (set the address here, "
                    + "or THEORIARR__METADATA__PROVIDARRBASEURL).";
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to query Providarr /v1/policy");

                return null;
            }
        }

        private static string FormatDuration(long? seconds)
        {
            if (seconds == null || seconds <= 0)
            {
                return "unknown";
            }

            var span = TimeSpan.FromSeconds(seconds.Value);

            if (span.TotalDays >= 1)
            {
                return $"{span.TotalDays:0.##} days";
            }

            if (span.TotalHours >= 1)
            {
                return $"{span.TotalHours:0.##} hours";
            }

            if (span.TotalMinutes >= 1)
            {
                return $"{span.TotalMinutes:0.##} minutes";
            }

            return $"{span.TotalSeconds:0.##} seconds";
        }

        private class ProvidarrPolicy
        {
            public CachePolicy Cache { get; set; }

            [JsonProperty("rate_limits")]
            public RateLimitPolicy RateLimits { get; set; }
        }

        private class CachePolicy
        {
            [JsonProperty("default_ttl_seconds")]
            public long DefaultTtlSeconds { get; set; }

            [JsonProperty("endpoint_ttl_seconds")]
            public Dictionary<string, long> EndpointTtlSeconds { get; set; }

            public long MetadataTtlSeconds()
            {
                if (EndpointTtlSeconds != null)
                {
                    foreach (var key in new[] { "movie", "series", "show", "tv" })
                    {
                        if (EndpointTtlSeconds.TryGetValue(key, out var ttl))
                        {
                            return ttl;
                        }
                    }
                }

                return DefaultTtlSeconds;
            }
        }

        private class RateLimitPolicy
        {
            public InboundPolicy Inbound { get; set; }
        }

        private class InboundPolicy
        {
            [JsonProperty("requests_per_second")]
            public double RequestsPerSecond { get; set; }

            [JsonProperty("burst")]
            public int Burst { get; set; }
        }
    }
}
