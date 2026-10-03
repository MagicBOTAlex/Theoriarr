using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/metadatasource")]
[AppSubsystem(AppSubsystem.Series)]
public class MetadataSourceSettingsController : SettingsController<MetadataSourceSettingsResource>
{
    private readonly IMetadataProviderStatusService _statusService;

    public MetadataSourceSettingsController(IConfigFileProvider configFileProvider, IConfigService configService, IMetadataProviderStatusService statusService)
        : base(configFileProvider, configService)
    {
        _statusService = statusService;
    }

    protected override MetadataSourceSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return MetadataSourceSettingsResourceMapper.ToResource(model, _statusService);
    }
}
