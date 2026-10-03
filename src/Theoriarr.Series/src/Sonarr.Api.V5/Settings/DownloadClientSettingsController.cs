using NzbDrone.Core.Configuration;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/downloadclient")]
[AppSubsystem(AppSubsystem.Series)]
public class DownloadClientSettingsController : SettingsController<DownloadClientSettingsResource>
{
    public DownloadClientSettingsController(IConfigFileProvider configFileProvider, IConfigService configService)
        : base(configFileProvider, configService)
    {
    }

    protected override DownloadClientSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return DownloadClientSettingsResourceMapper.ToResource(model);
    }
}
