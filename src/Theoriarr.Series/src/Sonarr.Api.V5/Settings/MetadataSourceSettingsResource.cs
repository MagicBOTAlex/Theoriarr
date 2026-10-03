using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Settings;

public class MetadataSourceSettingsResource : RestResource
{
    // Editable: base URL of the Providarr instance (empty = shared public default).
    public string? ProvidarrBaseUrl { get; set; }

    // Read-only: the URL actually in effect for this process (env/JSON override the stored value).
    public string? ResolvedProvidarrBaseUrl { get; set; }

    // Read-only: warning shown when the shared public instance is in use.
    public string? Warning { get; set; }
}

public static class MetadataSourceSettingsResourceMapper
{
    public static MetadataSourceSettingsResource ToResource(IConfigService model, IMetadataProviderStatusService statusService)
    {
        var status = statusService.GetStatus();

        return new MetadataSourceSettingsResource
        {
            ProvidarrBaseUrl = model.ProvidarrBaseUrl,
            ResolvedProvidarrBaseUrl = status.BaseUrl,
            Warning = status.Warning
        };
    }
}
