using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Test.Subsystem
{
    [TestFixture]
    public class ApiKeyResolverFixture
    {
        private const string SeriesKey = "series-api-key";
        private const string MovieKey = "movie-api-key";

        private static ApiKeyResolver BuildSubject()
        {
            return new ApiKeyResolver(new SubsystemConfigStub());
        }

        private static HttpContext BuildContext(string queryKey = null, string headerKey = null)
        {
            var context = new DefaultHttpContext();

            if (queryKey != null)
            {
                context.Request.QueryString = new QueryString($"?apikey={queryKey}");
            }

            if (headerKey != null)
            {
                context.Request.Headers["X-Api-Key"] = headerKey;
            }

            return context;
        }

        [Test]
        public void should_resolve_series_key()
        {
            BuildSubject().Resolve(SeriesKey).Should().Be(AppSubsystem.Series);
        }

        [Test]
        public void should_resolve_movie_key()
        {
            BuildSubject().Resolve(MovieKey).Should().Be(AppSubsystem.Movies);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-key")]
        public void should_not_resolve_absent_or_unknown_key(string apiKey)
        {
            BuildSubject().Resolve(apiKey).Should().BeNull();
        }

        [Test]
        public void resolve_for_request_or_null_should_return_null_for_an_unrecognised_key()
        {
            var subject = BuildSubject();

            subject.ResolveForRequestOrNull(BuildContext(headerKey: "not-a-key").Request).Should().BeNull();
            subject.ResolveForRequestOrNull(BuildContext().Request).Should().BeNull();
        }

        [Test]
        public void resolve_for_request_or_null_should_resolve_a_recognised_key()
        {
            var subject = BuildSubject();

            subject.ResolveForRequestOrNull(BuildContext(headerKey: MovieKey).Request).Should().Be(AppSubsystem.Movies);
            subject.ResolveForRequestOrNull(BuildContext(queryKey: SeriesKey).Request).Should().Be(AppSubsystem.Series);
        }

        [Test]
        public void resolve_for_request_should_default_to_series_without_a_key()
        {
            BuildSubject().ResolveForRequest(BuildContext().Request).Should().Be(AppSubsystem.Series);
            BuildSubject().ResolveForRequest(BuildContext(headerKey: "not-a-key").Request).Should().Be(AppSubsystem.Series);
        }

        private class SubsystemConfigStub : ISubsystemConfig
        {
            public string SeriesApiKey => SeriesKey;
            public string MovieApiKey => MovieKey;
        }
    }
}
