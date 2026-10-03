using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Analytics;
using NzbDrone.Core.Configuration;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Frontend
{
    // The bootstrap carries the API keys, so it must not be readable anonymously. The
    // "Initialize" policy is the UI policy plus BootstrapKeyAccessRequirement: a real login
    // always passes, while the anonymous "no authentication" principal only passes from a
    // local (loopback/LAN) client. A public client with authentication disabled is refused,
    // closing the anonymous key exposure without breaking the local SPA bootstrap.
    [Authorize(Policy = "Initialize")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class InitializeJsonController : Controller
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IAnalyticsService _analyticsService;
        private readonly ISubsystemInfo _subsystemInfo;

        private static string _apiKey;
        private static string _movieApiKey;
        private static string _urlBase;
        private string _generatedContent;

        public InitializeJsonController(IConfigFileProvider configFileProvider,
                                      IAnalyticsService analyticsService,
                                      ISubsystemInfo subsystemInfo)
        {
            _configFileProvider = configFileProvider;
            _analyticsService = analyticsService;
            _subsystemInfo = subsystemInfo;

            _apiKey = configFileProvider.ApiKey;
            _movieApiKey = configFileProvider.MovieApiKey;
            _urlBase = configFileProvider.UrlBase;
        }

        // Legacy single-app bootstrap (series shaped) kept for existing clients.
        [HttpGet("/initialize.json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public ContentHttpResult Index()
        {
            return TypedResults.Content(GetContent(), "application/json");
        }

        // Single combined bootstrap for the unified SPA. Reachable before login and the
        // only endpoint the frontend calls (contract in exec3-frontend.md).
        [HttpGet("/api/v3/initialize.json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public ContentHttpResult Unified()
        {
            return TypedResults.Content(GetContent(), "application/json");
        }

        private string GetContent()
        {
            if (RuntimeInfo.IsProduction && _generatedContent != null)
            {
                return _generatedContent;
            }

            var apiRoot = $"{_urlBase}/api/v3";
            var seriesApiRoot = $"{_urlBase}/api/v5";
            var theme = _configFileProvider.Theme;
            var isProduction = RuntimeInfo.IsProduction;

            var payload = new
            {
                urlBase = _urlBase,
                isUnified = true,
                release = BuildInfo.Release,
                version = BuildInfo.Version.ToString(),
                branch = _configFileProvider.Branch.ToLowerInvariant(),
                services = new
                {
                    movies = new
                    {
                        apiRoot,
                        apiKey = _movieApiKey,
                        appName = AppSubsystemExtensions.MoviesAppName,
                        instanceName = "Movies",
                        version = _subsystemInfo.GetVersion(AppSubsystem.Movies).ToString(),
                        theme,
                        signalrRoot = $"{_urlBase}/signalr/movies",
                        isProduction
                    },
                    series = new
                    {
                        apiRoot = seriesApiRoot,
                        apiKey = _apiKey,
                        appName = AppSubsystemExtensions.SeriesAppName,
                        instanceName = "Series",
                        version = _subsystemInfo.GetVersion(AppSubsystem.Series).ToString(),
                        theme,
                        signalrRoot = $"{_urlBase}/signalr/series",
                        isProduction
                    }
                },

                // Legacy top-level fields retained for older clients of /initialize.json.
                apiRoot,
                apiKey = _apiKey,
                movieApiKey = _movieApiKey,
                appName = BuildInfo.AppName,
                clientApiRoots = new { movies = apiRoot, series = seriesApiRoot },
                instanceName = _configFileProvider.InstanceName,
                analytics = _analyticsService.IsEnabled,
                userHash = HashUtil.AnonymousToken(),
                isProduction
            };

            _generatedContent = STJson.ToJson(payload);

            return _generatedContent;
        }
    }
}
