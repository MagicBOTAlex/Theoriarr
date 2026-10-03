using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Tags;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Tags
{
    [V3ApiController("tag/detail")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class TagDetailsController : RestController<TagDetailsResource>
    {
        private readonly ITagService _tagService;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public TagDetailsController(ITagService tagService, ISubsystemAccessor subsystemAccessor)
        {
            _tagService = tagService;
            _subsystemAccessor = subsystemAccessor;
        }

        protected override TagDetailsResource GetResourceById(int id)
        {
            var details = _tagService.Details(id);

            if (details == null || !IsVisible(details))
            {
                throw new NotFoundException();
            }

            return details.ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<TagDetailsResource> GetAll()
        {
            // Keep the same never-empty setup fallback as TagController: the movie key omits
            // tags referenced only by series, but must not end up with an empty list.
            var all = _tagService.Details().ToList();
            var visible = all.Where(IsVisible).ToList();

            return SubsystemDomainScope.WithSetupFallback(_subsystemAccessor.Subsystem, all, visible).ToResource();
        }

        private bool IsVisible(TagDetails details)
        {
            var tag = _tagService.GetTag(details.Id);
            var mediaType = tag?.MediaType ?? MediaType.Series;

            return SubsystemDomainScope.IsVisibleTo(
                _subsystemAccessor.Subsystem,
                mediaType,
                details.SeriesIds.Any(),
                details.MovieIds.Any());
        }
    }
}
