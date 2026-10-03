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

namespace Sonarr.Api.V3.ImportLists
{
    // D6 — the SERIES exclusion route, now backed by the merged MediaType-tagged store. It is
    // dual-tagged so the movie key does not 404 on the legacy path, but it only ever exposes
    // the series/anime subset (the movie route `/exclusions` owns MediaType.Movie).
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class ImportListExclusionController : RestController<ImportListExclusionResource>
    {
        private readonly IImportListExclusionService _importListExclusionService;

        public ImportListExclusionController(IImportListExclusionService importListExclusionService,
                                             ImportListExclusionExistsValidator importListExclusionExistsValidator)
        {
            _importListExclusionService = importListExclusionService;

            SharedValidator.RuleFor(c => c.TvdbId).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .SetValidator(importListExclusionExistsValidator);

            SharedValidator.RuleFor(c => c.Title).NotEmpty();
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

        [HttpGet]
        [Produces("application/json")]
        [Obsolete("Deprecated")]
        public List<ImportListExclusionResource> GetImportListExclusions()
        {
            return _importListExclusionService.All().Where(IsVisible).ToResource();
        }

        [HttpGet("paged")]
        [Produces("application/json")]
        public PagingResource<ImportListExclusionResource> GetImportListExclusionsPaged([FromQuery] PagingRequestResource paging)
        {
            var pagingResource = new PagingResource<ImportListExclusionResource>(paging);
            var pageSpec = pagingResource.MapToPagingSpec<ImportListExclusionResource, ImportListExclusion>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "id",
                    "title",
                    "tvdbId"
                },
                "id",
                SortDirection.Descending);

            pageSpec.FilterExpressions.Add(e => e.MediaType != MediaType.Movie);

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
            model.MediaType = existing.MediaType;
            _importListExclusionService.Update(model);

            return Accepted(resource.Id);
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
            return exclusion.MediaType != MediaType.Movie;
        }
    }
}
