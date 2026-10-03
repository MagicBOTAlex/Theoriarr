using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.MovieStats;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Movies.Wanted
{
    [V3ApiController("wanted/missing")]
    [AppSubsystem(AppSubsystem.Movies)]
    public class MissingController : MovieControllerWithSignalR
    {
        public MissingController(IMovieService movieService,
                            IMovieTranslationService movieTranslationService,
                            IMovieStatisticsService movieStatisticsService,
                            IUpgradableSpecification upgradableSpecification,
                            ICustomFormatCalculationService formatCalculator,
                            IConfigService configService,
                            IMovieCoversToLocal coverMapper,
                            IBroadcastSignalRMessage signalRBroadcaster)
            : base(movieService, movieTranslationService, movieStatisticsService, upgradableSpecification, formatCalculator, configService, coverMapper, signalRBroadcaster)
        {
        }

        [NonAction]
        public override Results<Ok<MovieResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<MovieResource> GetMissingMovies([FromQuery] PagingRequestResource paging, bool monitored = true)
        {
            var pagingResource = new PagingResource<MovieResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<MovieResource, Movie>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "movieMetadata.digitalRelease",
                    "movieMetadata.inCinemas",
                    "movieMetadata.physicalRelease",
                    "movieMetadata.sortTitle",
                    "movieMetadata.year",
                    "movies.lastSearchTime"
                },
                "movieMetadata.sortTitle",
                SortDirection.Ascending);

            pagingSpec.FilterExpressions.Add(v => v.Monitored == monitored);

            var resource = pagingSpec.ApplyToPage(_movieService.MoviesWithoutFiles, v => MapToResource(v));

            return resource;
        }
    }
}
