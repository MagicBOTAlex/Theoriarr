using System;
using NzbDrone.Common.Http;

namespace NzbDrone.Common.Options;

/// <summary>
/// Points movie/series metadata at a self-hosted Providarr instance.
///
/// Theoriarr does not depend on the upstream Sonarr/Radarr-operated metadata servers; their
/// URLs are no longer compiled in. Set <see cref="ProvidarrBaseUrl"/> to your own instance,
/// or leave it unset to use the shared public default.
/// </summary>
public class MetadataOptions
{
    /// <summary>The host that always gets progressive retry/backoff.</summary>
    public const string ForcedRetryHost = "providarr.deprived.dev";

    /// <summary>Base URL used when <see cref="ProvidarrBaseUrl"/> is not configured.</summary>
    public const string DefaultProvidarrBaseUrl = "https://providarr.deprived.dev";

    /// <summary>Base URL of the self-hosted provider, e.g. https://providarr.deprived.dev.</summary>
    public string ProvidarrBaseUrl { get; set; }

    /// <summary>
    /// Optional full URL template overriding the movie metadata endpoint.
    /// The <c>{route}</c> placeholder is preserved (e.g. "https://host/radarr/v1/{route}").
    /// </summary>
    public string MovieUrl { get; set; }

    /// <summary>
    /// Optional full URL template overriding the series metadata endpoint.
    /// The <c>{route}</c> and <c>{language}</c> placeholders are preserved
    /// (e.g. "https://host/sonarr/v1/tvdb/{route}/{language}/").
    /// </summary>
    public string SeriesUrl { get; set; }

    /// <summary>
    /// TMDb API read token used for TMDb-backed features (e.g. import lists).
    /// When unset, the legacy built-in token is used.
    /// Set via <c>Theoriarr:Metadata:TmdbApiKey</c> (env
    /// <c>THEORIARR__METADATA__TMDBAPIKEY</c>).
    /// </summary>
    public string TmdbApiKey { get; set; }

    /// <summary>
    /// Optional base URL for the ancillary Sonarr "services" (time, ping).
    /// When set it overrides the Providarr-derived default. Trailing slash is preserved
    /// as-is; routes are appended as <c>/{route}</c>.
    /// </summary>
    public string ServicesUrl { get; set; }

    /// <summary>The Providarr base URL, with surrounding whitespace and a trailing slash trimmed and the public default applied.</summary>
    public string ResolveBaseUrl()
    {
        var configured = ProvidarrBaseUrl?.Trim();

        return string.IsNullOrWhiteSpace(configured)
            ? DefaultProvidarrBaseUrl
            : configured.TrimEnd('/');
    }

    /// <summary>The effective movie metadata endpoint, honouring a trimmed <see cref="MovieUrl"/> override.</summary>
    public string ResolveMovieUrl()
    {
        var overrideUrl = MovieUrl?.Trim();

        return string.IsNullOrWhiteSpace(overrideUrl)
            ? $"{ResolveBaseUrl()}/radarr/v1/{{route}}"
            : overrideUrl;
    }

    /// <summary>The effective series metadata endpoint, honouring a trimmed <see cref="SeriesUrl"/> override.</summary>
    public string ResolveSeriesUrl()
    {
        var overrideUrl = SeriesUrl?.Trim();

        return string.IsNullOrWhiteSpace(overrideUrl)
            ? $"{ResolveBaseUrl()}/sonarr/v1/tvdb/{{route}}/{{language}}/"
            : overrideUrl;
    }

    /// <summary>The effective ancillary services endpoint, honouring a trimmed <see cref="ServicesUrl"/> override.</summary>
    public string ResolveServicesUrl()
    {
        var overrideUrl = ServicesUrl?.Trim();

        return string.IsNullOrWhiteSpace(overrideUrl)
            ? $"{ResolveBaseUrl()}/sonarr/services/"
            : overrideUrl;
    }

    /// <summary>Safely parses a URL, returning <c>false</c> instead of throwing on invalid input.</summary>
    public static bool TryGetUri(string url, out HttpUri uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            uri = new HttpUri(url.Trim());

            return !string.IsNullOrWhiteSpace(uri.Host);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Safely resolves the host of a URL, returning <c>false</c> instead of throwing on invalid input.</summary>
    public static bool TryGetHost(string url, out string host)
    {
        host = null;

        if (TryGetUri(url, out var uri))
        {
            host = uri.Host;

            return true;
        }

        return false;
    }

    /// <summary>Safely resolves the scheme+host(+port) origin of a URL, returning <c>false</c> instead of throwing on invalid input.</summary>
    public static bool TryGetOrigin(string url, out string origin)
    {
        origin = null;

        if (!TryGetUri(url, out var uri))
        {
            return false;
        }

        var scheme = string.IsNullOrWhiteSpace(uri.Scheme) ? "https" : uri.Scheme;

        origin = uri.Port.HasValue
            ? $"{scheme}://{uri.Host}:{uri.Port}"
            : $"{scheme}://{uri.Host}";

        return true;
    }

    /// <summary>
    /// Apply progressive retry/backoff to metadata requests aimed at alternative
    /// (non-default) metadata hosts. Requests to <see cref="ForcedRetryHost"/> are always
    /// retried regardless of this flag.
    /// </summary>
    public bool RetryAlternativeProviders { get; set; }

    /// <summary>Hard upper bound applied to <see cref="RetryMaxRetries"/> so a bad value can't cause an unbounded retry loop.</summary>
    public const int MaxRetryLimit = 10;

    /// <summary>Smallest base delay, in seconds, that will be used (a zero-delay retry loop is never allowed).</summary>
    public const double MinRetryBaseDelaySeconds = 0.1;

    /// <summary>Additional attempts after the first failure (5 = up to 6 attempts).</summary>
    public int RetryMaxRetries { get; set; } = 5;

    /// <summary>Initial retry delay in seconds (doubles each attempt).</summary>
    public double RetryBaseDelaySeconds { get; set; } = 1.0;

    /// <summary>Upper bound for a single retry delay, in seconds.</summary>
    public double RetryMaxDelaySeconds { get; set; } = 60.0;

    /// <summary>Fractional jitter applied to each retry delay (0.2 = +/-20%).</summary>
    public double RetryJitter { get; set; } = 0.2;

    /// <summary>
    /// Builds the retry policy for a metadata host, or <c>null</c> when retries should not apply.
    /// Forced for <see cref="ForcedRetryHost"/>; otherwise opt-in via
    /// <see cref="RetryAlternativeProviders"/>.
    /// </summary>
    public HttpRetryPolicy ResolveRetryPolicy(string host)
    {
        var forced = string.Equals(host, ForcedRetryHost, StringComparison.OrdinalIgnoreCase);

        if (!forced && !RetryAlternativeProviders)
        {
            return null;
        }

        var baseDelaySeconds = Math.Max(MinRetryBaseDelaySeconds, RetryBaseDelaySeconds);
        var maxDelaySeconds = Math.Max(baseDelaySeconds, RetryMaxDelaySeconds);

        return new HttpRetryPolicy
        {
            Enabled = true,
            MaxRetries = Math.Clamp(RetryMaxRetries, 0, MaxRetryLimit),
            BaseDelay = TimeSpan.FromSeconds(baseDelaySeconds),
            MaxDelay = TimeSpan.FromSeconds(maxDelaySeconds),
            Jitter = Math.Clamp(RetryJitter, 0.0, 1.0),
        };
    }
}
