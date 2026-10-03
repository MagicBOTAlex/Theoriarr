using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Sonarr.Http.Subsystem
{
    public class SubsystemMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        private readonly IApiKeyResolver _apiKeyResolver;

        public SubsystemMatcherPolicy(IApiKeyResolver apiKeyResolver)
        {
            _apiKeyResolver = apiKeyResolver;
        }

        public override int Order => 0;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            return endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<IAppSubsystemMetadata>() != null);
        }

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            // A key that is recognised but belongs to the other domain still 404s, so a valid
            // key never reaches the wrong subsystem's handlers.
            var subsystem = _apiKeyResolver.ResolveForRequestOrNull(httpContext.Request);

            if (subsystem.HasValue)
            {
                InvalidateCandidatesOtherThan(candidates, subsystem.Value);

                return Task.CompletedTask;
            }

            // A request with no key or an unrecognised key must be rejected by the
            // authentication layer with a uniform 401. Routing runs before authentication, so
            // invalidating a single non-default candidate here would surface as a routing 404.
            // On a route shared by more than one subsystem (e.g. /api/v3/queue) leaving every
            // candidate valid makes endpoint selection ambiguous and returns 500 instead, so the
            // non-default candidates are invalidated -- but only when at least one candidate
            // (the default subsystem, or an untagged endpoint) remains to reach the 401.
            var defaultSubsystem = AppSubsystem.Series;

            if (!HasCandidateFor(candidates, defaultSubsystem))
            {
                return Task.CompletedTask;
            }

            InvalidateCandidatesOtherThan(candidates, defaultSubsystem);

            return Task.CompletedTask;
        }

        private static bool HasCandidateFor(CandidateSet candidates, AppSubsystem subsystem)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var metadata = candidates[i].Endpoint?.Metadata.GetOrderedMetadata<IAppSubsystemMetadata>();

                if (metadata == null || metadata.Count == 0 || metadata.Any(m => m.Subsystem == subsystem))
                {
                    return true;
                }
            }

            return false;
        }

        private static void InvalidateCandidatesOtherThan(CandidateSet candidates, AppSubsystem subsystem)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var metadata = candidates[i].Endpoint?.Metadata.GetOrderedMetadata<IAppSubsystemMetadata>();

                if (metadata != null && metadata.Count > 0 && !metadata.Any(m => m.Subsystem == subsystem))
                {
                    candidates.SetValidity(i, false);
                }
            }
        }
    }
}
