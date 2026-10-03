using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.AlternativeTitles;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Movies
{
    [AppSubsystem(AppSubsystem.Movies)]
    [V3ApiController("alttitle")]
    public class AlternativeTitleController : RestController<AlternativeTitleResource>
    {
        private readonly IAlternativeTitleService _altTitleService;
        private readonly IMovieService _movieService;

        public AlternativeTitleController(IAlternativeTitleService altTitleService, IMovieService movieService)
        {
            _altTitleService = altTitleService;
            _movieService = movieService;
        }

        protected override AlternativeTitleResource GetResourceById(int id)
        {
            return _altTitleService.GetById(id).ToResource();
        }

        [HttpGet]
        public List<AlternativeTitleResource> GetAltTitles(int? movieId, int? movieMetadataId)
        {
            if (movieMetadataId.HasValue)
            {
                return _altTitleService.GetAllTitlesForMovieMetadata(movieMetadataId.Value).ToResource();
            }

            if (movieId.HasValue)
            {
                var movie = _movieService.GetMovie(movieId.Value);
                return _altTitleService.GetAllTitlesForMovieMetadata(movie.MovieMetadataId).ToResource();
            }

            return _altTitleService.GetAllTitles().ToResource();
        }
    }
}
