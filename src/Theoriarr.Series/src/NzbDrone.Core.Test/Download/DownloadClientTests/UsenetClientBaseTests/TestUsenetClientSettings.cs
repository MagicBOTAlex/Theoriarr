using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.UsenetClientBaseTests
{
    public class TestUsenetClientSettings : DownloadClientSettingsBase<TestUsenetClientSettings>
    {
        public override NzbDroneValidationResult Validate() => new();
    }
}
