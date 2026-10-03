using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Options;

namespace NzbDrone.Common.Test.Options
{
    [TestFixture]
    public class MetadataOptionsFixture
    {
        [Test]
        public void should_clamp_retry_max_retries()
        {
            var options = new MetadataOptions
            {
                RetryAlternativeProviders = true,
                RetryMaxRetries = 1000,
            };

            var policy = options.ResolveRetryPolicy("example.com");

            policy.MaxRetries.Should().Be(MetadataOptions.MaxRetryLimit);
        }

        [Test]
        public void should_enforce_positive_base_delay()
        {
            var options = new MetadataOptions
            {
                RetryAlternativeProviders = true,
                RetryBaseDelaySeconds = 0,
                RetryMaxDelaySeconds = 0,
            };

            var policy = options.ResolveRetryPolicy("example.com");

            policy.BaseDelay.Should().BeGreaterThan(TimeSpan.Zero);
            policy.MaxDelay.Should().BeGreaterThanOrEqualTo(policy.BaseDelay);
        }

        [Test]
        public void should_not_return_policy_when_alternative_providers_disabled()
        {
            var options = new MetadataOptions();

            options.ResolveRetryPolicy("example.com").Should().BeNull();
        }

        [Test]
        public void should_always_return_policy_for_forced_host()
        {
            var options = new MetadataOptions();

            options.ResolveRetryPolicy(MetadataOptions.ForcedRetryHost).Should().NotBeNull();
        }
    }
}
