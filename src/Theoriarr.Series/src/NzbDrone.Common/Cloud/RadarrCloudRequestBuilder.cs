using Microsoft.Extensions.Options;
using NzbDrone.Common.Http;
using NzbDrone.Common.Options;

namespace NzbDrone.Common.Cloud
{
    public interface IRadarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory TMDB { get; }
        IHttpRequestBuilderFactory RadarrMetadata { get; }
    }

    public class RadarrCloudRequestBuilder : IRadarrCloudRequestBuilder
    {
        // Legacy borrowed token, retained only as a fallback.
        // Override with MetadataOptions.TmdbApiKey (THEORIARR__METADATA__TMDBAPIKEY).
        private const string DefaultTmdbToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJhdWQiOiIxYTczNzMzMDE5NjFkMDNmOTdmODUzYTg3NmRkMTIxMiIsInN1YiI6IjU4NjRmNTkyYzNhMzY4MGFiNjAxNzUzNCIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.gh1BwogCCKOda6xj9FRMgAAj_RYKMMPC3oNlcBtlmwk";

        private readonly string _authToken;

        public RadarrCloudRequestBuilder(IOptions<MetadataOptions> metadataOptions)
        {
            var metadata = metadataOptions?.Value ?? new MetadataOptions();

            _authToken = string.IsNullOrWhiteSpace(metadata.TmdbApiKey)
                ? DefaultTmdbToken
                : metadata.TmdbApiKey.Trim();

            // TMDb is an independent third party (not a Sonarr/Radarr-operated server).
            TMDB = new HttpRequestBuilder("https://api.themoviedb.org/{api}/{route}/{id}{secondaryRoute}")
                .SetHeader("Authorization", $"Bearer {_authToken}")
                .CreateFactory();

            // Metadata always comes from Providarr; progressive retry is forced for the
            // public host and opt-in elsewhere.
            var metadataBuilder = new HttpRequestBuilder(metadata.ResolveMovieUrl());
            metadataBuilder.RetryPolicy = metadata.ResolveRetryPolicy(metadataBuilder.BaseUrl.Host);
            RadarrMetadata = metadataBuilder.CreateFactory();
        }

        public IHttpRequestBuilderFactory TMDB { get; private set; }
        public IHttpRequestBuilderFactory RadarrMetadata { get; private set; }

        public string AuthToken => _authToken;
    }
}
