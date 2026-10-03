using FluentValidation;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update;
using NzbDrone.Core.Validation.Paths;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/update")]
[AppSubsystem(AppSubsystem.Series)]
public class UpdateSettingsController : SettingsController<UpdateSettingsResource>
{
    public UpdateSettingsController(IConfigFileProvider configFileProvider, IConfigService configService)
        : base(configFileProvider, configService)
    {
        SharedValidator.RuleFor(c => c.UpdateScriptPath)
            .IsValidPath()
            .When(c => c.UpdateMechanism == UpdateMechanism.Script);
    }

    protected override UpdateSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return UpdateSettingsResourceMapper.ToResource(configFile);
    }
}
