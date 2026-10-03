using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Profiles.Qualities;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Profiles.Quality
{
    [V5ApiController("qualityprofile/schema")]
    [AppSubsystem(AppSubsystem.Series)]
    public class QualityProfileSchemaController : Controller
    {
        private readonly IQualityProfileService _profileService;

        public QualityProfileSchemaController(IQualityProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        [Produces("application/json")]
        public Ok<QualityProfileResource> GetSchema()
        {
            var qualityProfile = _profileService.GetDefaultProfile(string.Empty);

            return TypedResults.Ok(qualityProfile.ToResource());
        }
    }
}
