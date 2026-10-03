using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Library;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Library
{
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    [V3ApiController("library")]
    public class LibrarySearchController : RestController<LibrarySearchResource>
    {
        private readonly ILibrarySearchService _librarySearchService;

        public LibrarySearchController(ILibrarySearchService librarySearchService)
        {
            _librarySearchService = librarySearchService;
        }

        [NonAction]
        public override Results<Ok<LibrarySearchResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        protected override LibrarySearchResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet("search")]
        [Produces("application/json")]
        public Ok<List<LibrarySearchResource>> Search([FromQuery] string term)
        {
            return TypedResults.Ok(_librarySearchService.Search(term).ToResource());
        }
    }
}
