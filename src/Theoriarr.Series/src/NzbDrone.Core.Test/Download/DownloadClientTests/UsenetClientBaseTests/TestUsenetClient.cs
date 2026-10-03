using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.UsenetClientBaseTests
{
    public class TestUsenetClient : UsenetClientBase<TestUsenetClientSettings>
    {
        public TestUsenetClient(IHttpClient httpClient,
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            IValidateNzbs nzbValidationService,
            Logger logger,
            ILocalizationService localizationService)
            : base(httpClient, configService, diskProvider, remotePathMappingService, nzbValidationService, logger, localizationService)
        {
        }

        public override string Name => "Test Usenet Client";

        public bool EpisodeNzbAdded { get; private set; }
        public bool MovieNzbAdded { get; private set; }

        protected override string AddFromNzbFile(RemoteEpisode remoteEpisode, string filename, byte[] fileContent)
        {
            EpisodeNzbAdded = true;
            return filename;
        }

        protected override string AddFromNzbFile(RemoteMovie remoteMovie, string filename, byte[] fileContent)
        {
            MovieNzbAdded = true;
            return filename;
        }

        public override IEnumerable<DownloadClientItem> GetItems() => Enumerable.Empty<DownloadClientItem>();

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
        }

        public override DownloadClientInfo GetStatus() => new() { IsLocalhost = true };

        protected override void Test(List<ValidationFailure> failures)
        {
        }
    }
}
