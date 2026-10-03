using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Movies;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Movies
{
    [AppSubsystem(AppSubsystem.Movies)]
    [V3ApiController("movie/import")]
    public class MovieImportController : RestController<MovieResource>
    {
        private readonly IAddMovieService _addMovieService;

        public MovieImportController(IAddMovieService addMovieService)
        {
            _addMovieService = addMovieService;
        }

        [NonAction]
        public override Results<Ok<MovieResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        protected override MovieResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IEnumerable<MovieResource> Import([FromBody] List<MovieResource> resource)
        {
            var newMovies = resource.ToModel();

            return _addMovieService.AddMovies(newMovies).ToResource(0);
        }
    }
}
