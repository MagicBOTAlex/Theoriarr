using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Profiles.Quality
{
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class QualityProfileController : RestController<QualityProfileResource>
    {
        private readonly IQualityProfileService _profileService;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public QualityProfileController(IQualityProfileService profileService,
                                        ICustomFormatService formatService,
                                        ISeriesService seriesService,
                                        IMovieService movieService,
                                        ISubsystemAccessor subsystemAccessor)
        {
            _profileService = profileService;
            _seriesService = seriesService;
            _movieService = movieService;
            _subsystemAccessor = subsystemAccessor;
            SharedValidator.RuleFor(c => c.Name).NotEmpty();

            SharedValidator.RuleFor(c => c.MinUpgradeFormatScore).GreaterThanOrEqualTo(1);
            SharedValidator.RuleFor(c => c.Cutoff).ValidCutoff();
            SharedValidator.RuleFor(c => c.Items).ValidItems();

            SharedValidator.RuleFor(c => c.FormatItems).Must(items =>
            {
                var all = formatService.All().Select(f => f.Id).ToList();
                var ids = items.Select(i => i.Format);

                return all.Except(ids).Empty();
            }).WithMessage("All Custom Formats and no extra ones need to be present inside your Profile! Try refreshing your browser.");

            SharedValidator.RuleFor(c => c).Custom((profile, context) =>
            {
                if (profile.FormatItems.Where(x => x.Score > 0).Sum(x => x.Score) < profile.MinFormatScore &&
                    profile.FormatItems.Max(x => x.Score) < profile.MinFormatScore)
                {
                    context.AddFailure("Minimum Custom Format Score can never be satisfied");
                }
            });

            SharedValidator.RuleFor(c => c)
                .SetValidator(new QualityProfileResourceValidator());
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<QualityProfileResource> Create([FromBody] QualityProfileResource resource)
        {
            var model = resource.ToModel();

            // QualityProfileResource does not expose MediaType, so stamp the resolved
            // domain here. Existing rows default to Series; a movie-key create is tagged
            // Movie so future reads can scope it.
            model.MediaType = _subsystemAccessor.Subsystem.ToMediaType();

            model = _profileService.Add(model);
            return Created(model.Id);
        }

        [RestDeleteById]
        public void DeleteProfile(int id)
        {
            if (!IsVisible(_profileService.Get(id)))
            {
                throw new NotFoundException();
            }

            _profileService.Delete(id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<QualityProfileResource> Update([FromBody] QualityProfileResource resource)
        {
            var model = resource.ToModel();

            // Resource mapping drops the discriminator; keep the stored MediaType so a
            // PUT does not silently reset an explicitly movie-tagged profile to Series.
            model.MediaType = _profileService.Get(model.Id).MediaType;

            // Return the just-written profile directly: a hidden row (e.g. an explicit Movie
            // profile reached via the fallback list) updates successfully, but re-reading it
            // through the domain-scoped GetResourceById would turn the committed PUT into a 404.
            _profileService.Update(model);

            return Accepted(model.ToResource());
        }

        protected override QualityProfileResource GetResourceById(int id)
        {
            var profile = _profileService.Get(id);

            if (profile == null || !IsVisible(profile))
            {
                throw new NotFoundException();
            }

            return profile.ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<QualityProfileResource> GetAll()
        {
            // Scope by domain without a schema change. The MediaType discriminator exists
            // but every seeded profile defaults to Series, so a strict MediaType filter
            // would empty the movie domain. Usage (which subsystem references the profile)
            // plus an explicit Movie tag gives correct-key-safe scoping, and
            // WithSetupFallback keeps the movie list non-empty when a shared profile is
            // already used by a series.
            var subsystem = _subsystemAccessor.Subsystem;
            var seriesProfileIds = new HashSet<int>(_seriesService.GetAllSeries().Select(s => s.QualityProfileId));
            var movieProfileIds = new HashSet<int>(_movieService.GetAllMovies().Select(m => m.QualityProfileId));

            var all = _profileService.All();
            var visible = all
                .Where(profile => SubsystemDomainScope.IsVisibleTo(
                    subsystem,
                    profile.MediaType,
                    seriesProfileIds.Contains(profile.Id),
                    movieProfileIds.Contains(profile.Id)))
                .ToList();

            return SubsystemDomainScope.WithSetupFallback(subsystem, all, visible).ToResource();
        }

        private bool IsVisible(QualityProfile profile)
        {
            var seriesProfileIds = new HashSet<int>(_seriesService.GetAllSeries().Select(s => s.QualityProfileId));
            var movieProfileIds = new HashSet<int>(_movieService.GetAllMovies().Select(m => m.QualityProfileId));

            return SubsystemDomainScope.IsVisibleTo(
                _subsystemAccessor.Subsystem,
                profile.MediaType,
                seriesProfileIds.Contains(profile.Id),
                movieProfileIds.Contains(profile.Id));
        }
    }
}
