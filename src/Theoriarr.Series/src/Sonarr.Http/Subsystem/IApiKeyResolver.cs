using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Sonarr.Http.Subsystem
{
    public interface IApiKeyResolver
    {
        AppSubsystem? Resolve(string apiKey);

        // Null when the request carries no key or an unrecognised one. Distinct from
        // ResolveForRequest, which defaults to Series so a request without a key is still
        // attributed to the series domain.
        AppSubsystem? ResolveForRequestOrNull(HttpRequest request);

        AppSubsystem ResolveForRequest(HttpRequest request);
        AppSubsystem ResolveForContext(HttpContext context);
    }

    public class ApiKeyResolver : IApiKeyResolver
    {
        public const string ApiKeyQueryName = "apikey";
        public const string ApiKeyHeaderName = "X-Api-Key";

        private readonly ISubsystemConfig _subsystemConfig;

        public ApiKeyResolver(ISubsystemConfig subsystemConfig)
        {
            _subsystemConfig = subsystemConfig;
        }

        public AppSubsystem? Resolve(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            if (FixedTimeEquals(apiKey, _subsystemConfig.MovieApiKey))
            {
                return AppSubsystem.Movies;
            }

            if (FixedTimeEquals(apiKey, _subsystemConfig.SeriesApiKey))
            {
                return AppSubsystem.Series;
            }

            return null;
        }

        public AppSubsystem? ResolveForRequestOrNull(HttpRequest request)
        {
            return Resolve(ReadApiKey(request, ApiKeyQueryName, ApiKeyHeaderName));
        }

        public AppSubsystem ResolveForRequest(HttpRequest request)
        {
            return ResolveForRequestOrNull(request) ?? AppSubsystem.Series;
        }

        public AppSubsystem ResolveForContext(HttpContext context)
        {
            if (context == null)
            {
                return AppSubsystem.Series;
            }

            var stored = SubsystemContext.Get(context);

            if (stored.HasValue)
            {
                return stored.Value;
            }

            var resolved = ResolveForRequest(context.Request);
            SubsystemContext.Set(context, resolved);

            return resolved;
        }

        public static string ReadApiKey(HttpRequest request, string queryName, string headerName)
        {
            if (request.Query.TryGetValue(queryName, out var queryValue))
            {
                var value = queryValue.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (request.Headers.TryGetValue(headerName, out var headerValue))
            {
                var value = headerValue.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", string.Empty);
        }

        private static bool FixedTimeEquals(string provided, string configured)
        {
            if (string.IsNullOrWhiteSpace(provided) || string.IsNullOrWhiteSpace(configured))
            {
                return false;
            }

            var providedBytes = Encoding.UTF8.GetBytes(provided);
            var configuredBytes = Encoding.UTF8.GetBytes(configured);

            return providedBytes.Length == configuredBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
        }
    }
}
