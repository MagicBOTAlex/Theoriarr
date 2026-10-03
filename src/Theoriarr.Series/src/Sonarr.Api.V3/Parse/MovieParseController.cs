using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.Parser;
using Sonarr.Api.V3.CustomFormats;
using Sonarr.Api.V3.Movies;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Parse
{
    // MOVIES parser for the Radarr-compatible /api/v3/parse route. The SERIES ParseController
    // shares this route; SubsystemMatcherPolicy selects the handler by API key.
    [V3ApiController("parse")]
    [AppSubsystem(AppSubsystem.Movies)]
    public class MovieParseController : Controller
    {
        private readonly IParsingService _parsingService;
        private readonly IConfigService _configService;
        private readonly IRemoteMovieAggregationService _aggregationService;
        private readonly ICustomFormatCalculationService _formatCalculator;

        public MovieParseController(IParsingService parsingService,
                                    IConfigService configService,
                                    IRemoteMovieAggregationService aggregationService,
                                    ICustomFormatCalculationService formatCalculator)
        {
            _parsingService = parsingService;
            _configService = configService;
            _aggregationService = aggregationService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public MovieParseResource Parse(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var parsedMovieInfo = Parser.ParseMovieTitle(title);

            if (parsedMovieInfo == null)
            {
                return new MovieParseResource
                {
                    Title = title
                };
            }

            var remoteMovie = _parsingService.Map(parsedMovieInfo, "", 0);

            if (remoteMovie != null)
            {
                _aggregationService.Augment(remoteMovie);

                remoteMovie.CustomFormats = _formatCalculator.ParseCustomFormat(remoteMovie, 0);
                remoteMovie.CustomFormatScore = remoteMovie.Movie?.QualityProfile?.CalculateCustomFormatScore(remoteMovie.CustomFormats) ?? 0;

                return new MovieParseResource
                {
                    Title = title,
                    ParsedMovieInfo = remoteMovie.ParsedMovieInfo,
                    Movie = remoteMovie.Movie.ToResource(_configService.AvailabilityDelay),
                    Languages = remoteMovie.Languages,
                    CustomFormats = remoteMovie.CustomFormats?.ToResource(false),
                    CustomFormatScore = remoteMovie.CustomFormatScore
                };
            }

            return new MovieParseResource
            {
                Title = title,
                ParsedMovieInfo = parsedMovieInfo
            };
        }
    }
}
