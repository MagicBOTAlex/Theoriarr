using System;

namespace NzbDrone.Common.Http
{
    /// <summary>
    /// Progressive retry policy attached to an <see cref="HttpRequest"/>.
    ///
    /// When enabled, <see cref="HttpClient"/> retries requests that fail because the
    /// server is unreachable/times out, returns 429, or returns 5xx. The delay grows
    /// exponentially (with jitter) up to <see cref="MaxDelay"/>; a 429 <c>Retry-After</c>
    /// header, when present, takes precedence.
    /// </summary>
    public class HttpRetryPolicy
    {
        public bool Enabled { get; set; }

        /// <summary>Additional attempts after the first failure. 5 means up to 6 attempts.</summary>
        public int MaxRetries { get; set; } = 5;

        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>Fractional jitter applied to each delay (0.2 = +/-20%).</summary>
        public double Jitter { get; set; } = 0.2;

        public HttpRetryPolicy Clone()
        {
            return new HttpRetryPolicy
            {
                Enabled = Enabled,
                MaxRetries = MaxRetries,
                BaseDelay = BaseDelay,
                MaxDelay = MaxDelay,
                Jitter = Jitter,
            };
        }
    }
}
