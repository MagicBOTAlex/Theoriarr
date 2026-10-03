using System;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.ImportLists.RadarrList2.StevenLu
{
    public class StevenLu2Import : HttpImportListBase<StevenLu2Settings>
    {
        private readonly IHttpRequestBuilderFactory _radarrMetadata;

        public override string Name => "StevenLu List";

        public override ImportListType ListType => ImportListType.Other;
        public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(12);

        public override bool Enabled => true;
        public override bool EnableAuto => false;
        public override MediaType MediaType => NzbDrone.Core.ImportLists.MediaType.Movie;

        public StevenLu2Import(IRadarrCloudRequestBuilder requestBuilder,
                              IHttpClient httpClient,
                              IImportListStatusService importListStatusService,
                              IConfigService configService,
                              IParsingService parsingService,
                              Logger logger)
        : base(httpClient, importListStatusService, configService, parsingService, logger)
        {
            _radarrMetadata = requestBuilder.RadarrMetadata;
        }

        public override IImportListRequestGenerator GetRequestGenerator()
        {
            return new StevenLu2RequestGenerator()
            {
                Settings = Settings,
                Logger = _logger,
                HttpClient = _httpClient,
                RequestBuilder = _radarrMetadata
            };
        }

        public override IParseImportListMovieResponse GetMovieParser()
        {
            return new RadarrList2Parser();
        }
    }
}
