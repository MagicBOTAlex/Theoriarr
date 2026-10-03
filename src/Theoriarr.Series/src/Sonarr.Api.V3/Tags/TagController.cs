using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Tags
{
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class TagController : RestControllerWithSignalR<TagResource, Tag>,
                                 IHandle<TagsUpdatedEvent>,
                                 IHandle<AutoTagsUpdatedEvent>
    {
        private readonly ITagService _tagService;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public TagController(IBroadcastSignalRMessage signalRBroadcaster,
            ITagService tagService,
            ISeriesService seriesService,
            IMovieService movieService,
            ISubsystemAccessor subsystemAccessor)
            : base(signalRBroadcaster)
        {
            _tagService = tagService;
            _seriesService = seriesService;
            _movieService = movieService;
            _subsystemAccessor = subsystemAccessor;

            SharedValidator.RuleFor(c => c.Label).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Matches("^[a-z0-9-]+$", RegexOptions.IgnoreCase)
                .WithMessage("Allowed characters a-z, 0-9 and -");
        }

        protected override TagResource GetResourceById(int id)
        {
            var tag = _tagService.GetTag(id);

            if (tag == null || !IsVisible(tag))
            {
                throw new NotFoundException();
            }

            return tag.ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<TagResource> GetAll()
        {
            // Combine the stored discriminator with usage: a tag created under the movie
            // key (Movie) is movie-only, while legacy/backfilled Series(0) tags stay scoped
            // by which domain actually references them. WithSetupFallback keeps the movie
            // list from going empty when every shared tag is used by a series.
            var subsystem = _subsystemAccessor.Subsystem;
            var seriesTagIds = new HashSet<int>(_seriesService.GetAllSeriesTags().Values.SelectMany(t => t));
            var movieTagIds = new HashSet<int>(_movieService.AllMovieTags().Values.SelectMany(t => t));

            var all = _tagService.All();
            var visible = all
                .Where(tag => SubsystemDomainScope.IsVisibleTo(
                    subsystem,
                    tag.MediaType,
                    seriesTagIds.Contains(tag.Id),
                    movieTagIds.Contains(tag.Id)))
                .ToList();

            return SubsystemDomainScope.WithSetupFallback(subsystem, all, visible).ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<TagResource> Create([FromBody] TagResource resource)
        {
            var model = resource.ToModel();

            // TagResource does not expose MediaType, so stamp the resolved domain here.
            // TagService.Add returns an existing label instead of inserting, so a movie
            // referencing a TV-created tag still resolves to the same shared row.
            model.MediaType = _subsystemAccessor.Subsystem.ToMediaType();

            // Return the persisted/reused tag directly: a reused row may belong to the
            // other domain (e.g. an explicit Movie tag), and re-reading it through the
            // domain-scoped GetResourceById would yield a null body for the series key.
            var tag = _tagService.Add(model);
            return Created(tag.ToResource());
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<TagResource> Update([FromBody] TagResource resource)
        {
            var model = resource.ToModel();

            // Resource mapping drops the discriminator; keep the stored MediaType so a PUT
            // does not silently reset an explicitly movie-tagged tag to Series.
            model.MediaType = _tagService.GetTag(model.Id).MediaType;

            // Return the persisted tag directly: a hidden row (e.g. an explicit Movie tag
            // reached via the fallback list) updates successfully, but re-reading it through
            // the domain-scoped GetResourceById would turn the committed PUT into a 404.
            var tag = _tagService.Update(model);
            return Accepted(tag.ToResource());
        }

        [RestDeleteById]
        public void DeleteTag(int id)
        {
            var tag = _tagService.GetTag(id);

            if (tag == null || !IsVisible(tag))
            {
                throw new NotFoundException();
            }

            _tagService.Delete(id);
        }

        private bool IsVisible(Tag tag)
        {
            var seriesTagIds = new HashSet<int>(_seriesService.GetAllSeriesTags().Values.SelectMany(t => t));
            var movieTagIds = new HashSet<int>(_movieService.AllMovieTags().Values.SelectMany(t => t));

            return SubsystemDomainScope.IsVisibleTo(
                _subsystemAccessor.Subsystem,
                tag.MediaType,
                seriesTagIds.Contains(tag.Id),
                movieTagIds.Contains(tag.Id));
        }

        [NonAction]
        public void Handle(TagsUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }

        [NonAction]
        public void Handle(AutoTagsUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }
    }
}
