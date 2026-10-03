using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Exclusions;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.ImportLists;

// D6 — the SERIES V5 exclusion route, backed by the merged MediaType-tagged store. Movie rows
// (MediaType.Movie) live on the V3 `/exclusions` route and are filtered out here.
[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
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

        if (exclusion == null || exclusion.MediaType == MediaType.Movie)
        {
            throw new NotFoundException();
        }

        return exclusion.ToResource();
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<PagingResource<ImportListExclusionResource>> GetImportListExclusions([FromQuery] PagingRequestResource paging)
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

        return TypedResults.Ok(pageSpec.ApplyToPage(_importListExclusionService.Paged, ImportListExclusionResourceMapper.ToResource));
    }

    [RestPostById]
    [Consumes("application/json")]
    public Results<Created<ImportListExclusionResource>, NotFound> AddImportListExclusion([FromBody] ImportListExclusionResource resource)
    {
        var importListExclusion = _importListExclusionService.Add(resource.ToModel());

        return TypedCreated(importListExclusion.Id);
    }

    [RestPutById]
    [Consumes("application/json")]
    public Results<Accepted<ImportListExclusionResource>, NotFound> UpdateImportListExclusion([FromBody] ImportListExclusionResource resource)
    {
        var existing = _importListExclusionService.Get(resource.Id);

        if (existing == null || existing.MediaType == MediaType.Movie)
        {
            throw new NotFoundException();
        }

        var model = resource.ToModel();
        model.MediaType = existing.MediaType;
        _importListExclusionService.Update(model);

        return TypedAccepted(resource.Id);
    }

    [RestDeleteById]
    public NoContent DeleteImportListExclusion(int id)
    {
        var existing = _importListExclusionService.Get(id);

        if (existing == null || existing.MediaType == MediaType.Movie)
        {
            throw new NotFoundException();
        }

        _importListExclusionService.Delete(id);

        return TypedResults.NoContent();
    }

    [HttpDelete("bulk")]
    [Consumes("application/json")]
    public NoContent DeleteImportListExclusions([FromBody] ImportListExclusionBulkResource resource)
    {
        var ids = _importListExclusionService.All()
            .Where(e => e.MediaType != MediaType.Movie && resource.Ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        _importListExclusionService.Delete(ids);

        return TypedResults.NoContent();
    }
}
