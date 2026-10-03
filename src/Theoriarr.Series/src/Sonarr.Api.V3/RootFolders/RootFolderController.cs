using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.RootFolders
{
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class RootFolderController : RestControllerWithSignalR<RootFolderResource, RootFolder>
    {
        private readonly IRootFolderService _rootFolderService;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public RootFolderController(IRootFolderService rootFolderService,
                                ISeriesService seriesService,
                                IMovieService movieService,
                                ISubsystemAccessor subsystemAccessor,
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
            _seriesService = seriesService;
            _movieService = movieService;
            _subsystemAccessor = subsystemAccessor;

            // Path rules belong to creation only: an update keeps the folder's path and
            // only adjusts its MediaType, so the "already a root folder" check must not
            // run on PUT.
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
            var folder = _rootFolderService.Get(id, timeout);

            if (folder == null || !IsVisible(folder))
            {
                throw new NotFoundException();
            }

            return folder.ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<RootFolderResource> CreateRootFolder([FromBody] RootFolderResource rootFolderResource)
        {
            var model = rootFolderResource.ToModel();

            // The unified settings UI sends an explicit MediaType so one page can create
            // TV/Movies/Anime folders. Older clients omit it and keep the historical
            // behaviour: stamped from the API key's domain (movie key -> Movie).
            var mediaType = rootFolderResource.MediaType ?? _subsystemAccessor.Subsystem.ToMediaType();

            EnsureMediaTypeAllowed(mediaType);

            model.MediaType = mediaType;

            return Created(_rootFolderService.Add(model).Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<RootFolderResource> UpdateRootFolder([FromBody] RootFolderResource rootFolderResource)
        {
            var model = _rootFolderService.Get(rootFolderResource.Id, true);

            if (model == null || !IsVisible(model))
            {
                throw new NotFoundException();
            }

            // The unified settings UI can move an existing folder between TV/Anime/Movies
            // without deleting and recreating it. Only the discriminator changes; the path
            // and everything else stay as they are.
            if (rootFolderResource.MediaType.HasValue)
            {
                EnsureMediaTypeAllowed(rootFolderResource.MediaType.Value);
                model.MediaType = rootFolderResource.MediaType.Value;
            }

            _rootFolderService.Update(model);

            return Accepted(model.ToResource());
        }

        [HttpGet]
        [Produces("application/json")]
        public List<RootFolderResource> GetRootFolders([FromQuery] bool all = false)
        {
            // The unified settings UI asks for `all=true` so its single Media Management
            // page can list TV, Anime and Movies folders together and show each type. Only
            // the series (host) key may use it; a movie key must never enumerate the series
            // domain's folders, so it always gets the scoped list.
            if (all && _subsystemAccessor.Subsystem == AppSubsystem.Series)
            {
                return _rootFolderService.AllWithUnmappedFolders().ToResource();
            }

            // Combine the stored discriminator with usage: rows created under the movie
            // key (Movie) are movie-only, while the legacy/backfilled Series(0) rows stay
            // scoped by which domain references them. WithSetupFallback keeps the movie
            // list from going empty when every shared row is used by a series.
            var subsystem = _subsystemAccessor.Subsystem;
            var seriesPaths = _seriesService.GetAllSeriesPaths().Values;
            var moviePaths = _movieService.AllMoviePaths().Values;

            var allFolders = _rootFolderService.AllWithUnmappedFolders();
            var visible = allFolders
                .Where(folder => SubsystemDomainScope.IsVisibleTo(
                    subsystem,
                    folder.MediaType,
                    folder.Path.IsPathUnder(seriesPaths),
                    folder.Path.IsPathUnder(moviePaths)))
                .ToList();

            return SubsystemDomainScope.WithSetupFallback(subsystem, allFolders, visible).ToResource();
        }

        [RestDeleteById]
        public void DeleteFolder(int id)
        {
            var folder = _rootFolderService.Get(id, false);

            if (folder == null || !IsVisible(folder))
            {
                throw new NotFoundException();
            }

            _rootFolderService.Remove(id);
        }

        private bool IsVisible(RootFolder folder)
        {
            return SubsystemDomainScope.IsVisibleTo(
                _subsystemAccessor.Subsystem,
                folder.MediaType,
                folder.Path.IsPathUnder(_seriesService.GetAllSeriesPaths().Values),
                folder.Path.IsPathUnder(_movieService.AllMoviePaths().Values));
        }

        // A key may only write the media types it owns: the series key creates/labels TV and
        // Anime folders, the movie key only Movie folders. This stops a cross-domain key from
        // relabelling (and thereby stealing) the other domain's root folders.
        private void EnsureMediaTypeAllowed(MediaType mediaType)
        {
            var allowed = _subsystemAccessor.Subsystem == AppSubsystem.Movies
                ? mediaType == MediaType.Movie
                : mediaType is MediaType.Series or MediaType.Anime;

            if (!allowed)
            {
                throw new BadRequestException($"Media type '{mediaType}' is not valid for the requesting subsystem.");
            }
        }
    }
}
