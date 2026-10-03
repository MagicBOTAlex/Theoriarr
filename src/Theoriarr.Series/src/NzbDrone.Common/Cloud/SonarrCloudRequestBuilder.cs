using Microsoft.Extensions.Options;
using NzbDrone.Common.Http;
using NzbDrone.Common.Options;

namespace NzbDrone.Common.Cloud
{
    public interface ISonarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory TvdbMetadata { get; }
    }

    public class SonarrCloudRequestBuilder : ISonarrCloudRequestBuilder
    {
        public SonarrCloudRequestBuilder(IOptions<MetadataOptions> metadataOptions)
        {
            var metadata = metadataOptions?.Value ?? new MetadataOptions();

            // Metadata always comes from Providarr; progressive retry is forced for the
            // public host and opt-in elsewhere. Apply it consistently to the ancillary
            // services (time/ping) as well.
            var servicesBuilder = new HttpRequestBuilder(metadata.ResolveServicesUrl());
            servicesBuilder.RetryPolicy = metadata.ResolveRetryPolicy(servicesBuilder.BaseUrl.Host);
            Services = servicesBuilder.CreateFactory();

            // A custom SeriesUrl may omit the {language} segment; don't fail startup when it does.
            var tvdbBuilder = new HttpRequestBuilder(metadata.ResolveSeriesUrl())
                .SetSegment("language", "en", dontCheck: true);
            tvdbBuilder.RetryPolicy = metadata.ResolveRetryPolicy(tvdbBuilder.BaseUrl.Host);
            TvdbMetadata = tvdbBuilder.CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory TvdbMetadata { get; }
    }
}
