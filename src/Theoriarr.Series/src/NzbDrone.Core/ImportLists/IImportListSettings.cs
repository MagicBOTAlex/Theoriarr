using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.ImportLists
{
    // D5: BaseUrl is no longer a member requirement. Movie settings models use typed
    // base urls (BaseUrl/Link) or provider-specific links; series settings may still
    // declare an `override BaseUrl` freely.
    public interface IImportListSettings : IProviderConfig
    {
    }
}
