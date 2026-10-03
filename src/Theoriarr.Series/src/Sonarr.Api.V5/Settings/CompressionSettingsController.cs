using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/compression")]
[AppSubsystem(AppSubsystem.Series)]
[AppSubsystem(AppSubsystem.Movies)]
public class CompressionSettingsController : SettingsController<CompressionSettingsResource>
{
    private readonly ITranscodeService _transcodeService;

    public CompressionSettingsController(IConfigFileProvider configFileProvider, IConfigService configService, ITranscodeService transcodeService)
        : base(configFileProvider, configService)
    {
        _transcodeService = transcodeService;

        SharedValidator.RuleFor(c => c.MaxConcurrentJobs).GreaterThanOrEqualTo(1);
        SharedValidator.RuleFor(c => c.DefaultQualityValue).GreaterThanOrEqualTo(0);
        SharedValidator.RuleFor(c => c.DefaultReducePercent).InclusiveBetween(1, 99);
        SharedValidator.RuleFor(c => c.TranscodeNice).GreaterThanOrEqualTo(0);
    }

    public override Results<Accepted<CompressionSettingsResource>, NotFound> SaveSettings(CompressionSettingsResource resource)
    {
        var result = base.SaveSettings(resource);

        // More concurrency (or re-enabling compression) should pick up queued jobs immediately.
        _transcodeService.Wake();

        return result;
    }

    protected override CompressionSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return CompressionSettingsResourceMapper.ToResource(model);
    }
}
