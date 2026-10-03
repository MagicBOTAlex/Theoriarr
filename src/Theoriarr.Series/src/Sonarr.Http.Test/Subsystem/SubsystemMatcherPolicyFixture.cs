using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Moq;
using NUnit.Framework;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Test.Subsystem
{
    [TestFixture]
    public class SubsystemMatcherPolicyFixture
    {
        private static CandidateSet BuildCandidates(params AppSubsystem[] subsystems)
        {
            var endpoints = new List<Endpoint>();
            var values = new List<RouteValueDictionary>();
            var scores = new List<int>();

            for (var i = 0; i < subsystems.Length; i++)
            {
                var metadata = new EndpointMetadataCollection(new AppSubsystemAttribute(subsystems[i]));

                endpoints.Add(new Endpoint(null, metadata, subsystems[i].ToString()));
                values.Add(new RouteValueDictionary());
                scores.Add(0);
            }

            if (subsystems.Length == 0)
            {
                endpoints.Add(new Endpoint(null, new EndpointMetadataCollection(), "untagged"));
                values.Add(new RouteValueDictionary());
                scores.Add(0);
            }

            return new CandidateSet(endpoints.ToArray(), values.ToArray(), scores.ToArray());
        }

        private static (SubsystemMatcherPolicy Policy, HttpContext Context) BuildSubject(AppSubsystem? resolved)
        {
            var resolver = new Mock<IApiKeyResolver>();
            resolver.Setup(r => r.ResolveForRequestOrNull(It.IsAny<HttpRequest>())).Returns(resolved);

            return (new SubsystemMatcherPolicy(resolver.Object), new DefaultHttpContext());
        }

        [Test]
        public async Task should_reject_series_endpoint_when_movie_key_presented()
        {
            var (policy, context) = BuildSubject(AppSubsystem.Movies);
            var candidates = BuildCandidates(AppSubsystem.Series);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeFalse();
        }

        [Test]
        public async Task should_accept_series_endpoint_when_series_key_presented()
        {
            var (policy, context) = BuildSubject(AppSubsystem.Series);
            var candidates = BuildCandidates(AppSubsystem.Series);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeTrue();
        }

        [Test]
        public async Task should_reject_movie_endpoint_when_series_key_presented()
        {
            var (policy, context) = BuildSubject(AppSubsystem.Series);
            var candidates = BuildCandidates(AppSubsystem.Movies);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeFalse();
        }

        [Test]
        public async Task should_leave_tagged_endpoint_valid_when_key_is_absent_or_unrecognised()
        {
            // An absent/wrong key must be rejected by authentication (401), not by routing
            // (404), so the matcher must not invalidate a lone candidate.
            var (policy, context) = BuildSubject(null);
            var candidates = BuildCandidates(AppSubsystem.Movies);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeTrue();
        }

        [Test]
        public async Task should_leave_only_the_default_candidate_when_key_is_absent_and_route_is_shared()
        {
            // /api/v3/queue is registered by both the series and the movies subsystem. With no
            // key, keeping both candidates valid makes endpoint selection throw
            // AmbiguousMatchException (500); the default (series) candidate must survive so the
            // request still reaches authentication and returns the uniform 401.
            var (policy, context) = BuildSubject(null);
            var candidates = BuildCandidates(AppSubsystem.Series, AppSubsystem.Movies);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeTrue();
            candidates.IsValidCandidate(1).Should().BeFalse();
        }

        [Test]
        public async Task should_leave_single_movie_candidate_valid_when_key_is_absent()
        {
            // But a route with only a movies candidate must not be invalidated, otherwise it
            // would 404 instead of reaching the 401.
            var (policy, context) = BuildSubject(null);
            var candidates = BuildCandidates(AppSubsystem.Movies);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeTrue();
        }

        [Test]
        public async Task should_invalidate_the_other_candidate_when_a_cross_domain_key_is_presented()
        {
            // A recognised key still wins over the default fallback: the series candidate is
            // removed for a movie key even though both are on the same route.
            var (policy, context) = BuildSubject(AppSubsystem.Movies);
            var candidates = BuildCandidates(AppSubsystem.Series, AppSubsystem.Movies);

            await policy.ApplyAsync(context, candidates);

            candidates.IsValidCandidate(0).Should().BeFalse();
            candidates.IsValidCandidate(1).Should().BeTrue();
        }

        [Test]
        public async Task should_accept_dual_endpoint_for_either_key()
        {
            var (seriesPolicy, seriesContext) = BuildSubject(AppSubsystem.Series);
            var seriesCandidates = BuildCandidates();

            await seriesPolicy.ApplyAsync(seriesContext, seriesCandidates);
            seriesCandidates.IsValidCandidate(0).Should().BeTrue();

            var (moviePolicy, movieContext) = BuildSubject(AppSubsystem.Movies);
            var movieCandidates = BuildCandidates();

            await moviePolicy.ApplyAsync(movieContext, movieCandidates);
            movieCandidates.IsValidCandidate(0).Should().BeTrue();
        }
    }
}
