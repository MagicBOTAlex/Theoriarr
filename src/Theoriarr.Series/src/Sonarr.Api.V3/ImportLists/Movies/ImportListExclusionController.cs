using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Exclusions;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.ImportLists.Movies
{
    // D6 — the MOVIES exclusion route (`/exclusions`), now backed by the merged
    // MediaType-tagged store. It only ever exposes the movie subset; the series route
    // `/importlistexclusion` owns the rest. Response shape is unchanged (`tmdbId`,
    // `movieTitle`, `movieYear`).
    [V3ApiController("exclusions")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class ImportListExclusionController : RestController<ImportListExclusionResource>
    {
        private readonly IImportListExclusionService _importListExclusionService;

        public ImportListExclusionController(IImportListExclusionService importListExclusionService,
                                             ImportListExclusionExistsValidator importListExclusionExistsValidator)
        {
            _importListExclusionService = importListExclusionService;

            SharedValidator.RuleFor(c => c.TmdbId).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .SetValidator(importListExclusionExistsValidator);

            SharedValidator.RuleFor(c => c.MovieTitle).NotEmpty();
            SharedValidator.RuleFor(c => c.MovieYear).GreaterThanOrEqualTo(0);
        }

        [HttpGet]
        [Produces("application/json")]
        [Obsolete("Deprecated")]
        public List<ImportListExclusionResource> GetImportListExclusions()
        {
            return _importListExclusionService.All().Where(IsVisible).ToResource();
        }

        protected override ImportListExclusionResource GetResourceById(int id)
        {
            var exclusion = _importListExclusionService.Get(id);

            if (exclusion == null || !IsVisible(exclusion))
            {
                throw new NotFoundException();
            }

            return exclusion.ToResource();
        }

        [HttpGet("paged")]
        [Produces("application/json")]
        public PagingResource<ImportListExclusionResource> GetImportListExclusionsPaged([FromQuery] PagingRequestResource paging)
        {
            // `movieTitle` is not a column on the merged store (the title moved into `Title`),
            // so translate the historical sort key before mapping.
            if (string.Equals(paging.SortKey, "movieTitle", StringComparison.OrdinalIgnoreCase))
            {
                paging.SortKey = "title";
            }

            var pagingResource = new PagingResource<ImportListExclusionResource>(paging);
            var pageSpec = pagingResource.MapToPagingSpec<ImportListExclusionResource, ImportListExclusion>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "id",
                    "title",
                    "movieYear",
                    "tmdbId"
                },
                "id",
                SortDirection.Descending);

            pageSpec.FilterExpressions.Add(e => e.MediaType == MediaType.Movie);

            return pageSpec.ApplyToPage(_importListExclusionService.Paged, ImportListExclusionResourceMapper.ToResource);
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<ImportListExclusionResource> AddImportListExclusion([FromBody] ImportListExclusionResource resource)
        {
            var importListExclusion = _importListExclusionService.Add(resource.ToModel());

            return Created(importListExclusion.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<ImportListExclusionResource> UpdateImportListExclusion([FromBody] ImportListExclusionResource resource)
        {
            var existing = _importListExclusionService.Get(resource.Id);

            if (existing == null || !IsVisible(existing))
            {
                throw new NotFoundException();
            }

            var model = resource.ToModel();
            model.MediaType = MediaType.Movie;
            _importListExclusionService.Update(model);

            return Accepted(resource.Id);
        }

        [HttpPost("bulk")]
        public object AddImportListExclusions([FromBody] List<ImportListExclusionResource> resources)
        {
            var importListExclusions = _importListExclusionService.Add(resources.ToModel());

            return importListExclusions.ToResource();
        }

        [RestDeleteById]
        public void DeleteImportListExclusion(int id)
        {
            var existing = _importListExclusionService.Get(id);

            if (existing == null || !IsVisible(existing))
            {
                throw new NotFoundException();
            }

            _importListExclusionService.Delete(id);
        }

        [HttpDelete("bulk")]
        [Produces("application/json")]
        public object DeleteImportListExclusions([FromBody] ImportListExclusionBulkResource resource)
        {
            var ids = _importListExclusionService.All()
                .Where(IsVisible)
                .Where(e => resource.Ids.Contains(e.Id))
                .Select(e => e.Id)
                .ToList();

            _importListExclusionService.Delete(ids);

            return new { };
        }

        private static bool IsVisible(ImportListExclusion exclusion)
        {
            return exclusion.MediaType == MediaType.Movie;
        }
    }
}
