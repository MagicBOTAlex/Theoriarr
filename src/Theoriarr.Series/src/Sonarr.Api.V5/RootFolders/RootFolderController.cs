using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.RootFolders;

[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
public class RootFolderController : RestControllerWithSignalR<RootFolderResource, RootFolder>
{
    private readonly IRootFolderService _rootFolderService;

    public RootFolderController(IRootFolderService rootFolderService,
                            IBroadcastSignalRMessage signalRBroadcaster,
                            RootFolderValidator rootFolderValidator,
                            PathExistsValidator pathExistsValidator,
                            MappedNetworkDriveValidator mappedNetworkDriveValidator,
                            RecycleBinValidator recycleBinValidator,
                            StartupFolderValidator startupFolderValidator,
                            SystemFolderValidator systemFolderValidator,
                            FolderWritableValidator folderWritableValidator)
    : base(signalRBroadcaster)
    {
        _rootFolderService = rootFolderService;

        // Path rules belong to creation only: an update keeps the folder's path and only
        // adjusts its MediaType, so the "already a root folder" check must not run on PUT.
        PostValidator.RuleFor(c => c.Path)
            .Cascade(CascadeMode.Stop)
            .IsValidPath()
                       .SetValidator(rootFolderValidator)
                       .SetValidator(mappedNetworkDriveValidator)
                       .SetValidator(startupFolderValidator)
                       .SetValidator(recycleBinValidator)
                       .SetValidator(pathExistsValidator)
                       .SetValidator(systemFolderValidator)
                       .SetValidator(folderWritableValidator);
    }

    protected override RootFolderResource GetResourceById(int id)
    {
        var timeout = Request?.GetBooleanQueryParameter("timeout", true) ?? true;

        return _rootFolderService.Get(id, timeout).ToResource();
    }

    [RestPostById]
    [Consumes("application/json")]
    public Results<Created<RootFolderResource>, NotFound> CreateRootFolder([FromBody] RootFolderResource rootFolderResource)
    {
        var model = rootFolderResource.ToModel();

        // Honor an explicit MediaType from the unified settings UI; otherwise this
        // series-only V5 surface stamps Series (its historical default).
        model.MediaType = rootFolderResource.MediaType ?? MediaType.Series;

        return TypedCreated(_rootFolderService.Add(model).Id);
    }

    [RestPutById]
    [Consumes("application/json")]
    public Results<Accepted<RootFolderResource>, NotFound> UpdateRootFolder([FromBody] RootFolderResource rootFolderResource)
    {
        var model = _rootFolderService.Get(rootFolderResource.Id, true);

        if (model == null)
        {
            throw new NotFoundException();
        }

        // The unified settings UI can move an existing folder between TV/Anime/Movies
        // without deleting and recreating it. Only the discriminator changes; the path
        // and everything else stay as they are.
        if (rootFolderResource.MediaType.HasValue)
        {
            model.MediaType = rootFolderResource.MediaType.Value;
        }

        _rootFolderService.Update(model);

        return TypedAccepted(model.ToResource());
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<RootFolderResource>> GetRootFolders()
    {
        return TypedResults.Ok(_rootFolderService.AllWithUnmappedFolders().ToResource());
    }

    [RestDeleteById]
    public NoContent DeleteFolder(int id)
    {
        _rootFolderService.Remove(id);

        return TypedResults.NoContent();
    }
}
