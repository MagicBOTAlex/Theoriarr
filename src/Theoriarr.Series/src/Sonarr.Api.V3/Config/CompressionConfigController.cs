using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Config
{
    [V3ApiController("config/compression")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class CompressionConfigController : ConfigController<CompressionConfigResource>
    {
        private readonly ITranscodeService _transcodeService;

        public CompressionConfigController(IConfigService configService, ITranscodeService transcodeService)
            : base(configService)
        {
            _transcodeService = transcodeService;

            SharedValidator.RuleFor(c => c.MaxConcurrentJobs).GreaterThanOrEqualTo(1);
            SharedValidator.RuleFor(c => c.DefaultQualityValue).GreaterThanOrEqualTo(0);
            SharedValidator.RuleFor(c => c.DefaultReducePercent).InclusiveBetween(1, 99);
            SharedValidator.RuleFor(c => c.TranscodeNice).GreaterThanOrEqualTo(0);
        }

        [RestPutById]
        public override ActionResult<CompressionConfigResource> SaveConfig([FromBody] CompressionConfigResource resource)
        {
            var result = base.SaveConfig(resource);

            // More concurrency (or re-enabling compression) should pick up queued jobs immediately.
            _transcodeService.Wake();

            return result;
        }

        protected override CompressionConfigResource ToResource(IConfigService model)
        {
            return CompressionConfigResourceMapper.ToResource(model);
        }
    }
}
