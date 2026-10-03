using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Options;

namespace NzbDrone.Common.Test.Cloud
{
    [TestFixture]
    public class MetadataSourceRequestBuilderFixture
    {
        [Test]
        public void should_default_movie_metadata_to_the_public_providarr()
        {
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(new MetadataOptions()));

            var request = builder.RadarrMetadata.Create()
                .SetSegment("route", "movie")
                .Resource("535167")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.deprived.dev/radarr/v1/movie/535167");
        }

        [Test]
        public void should_point_movie_metadata_at_a_custom_providarr()
        {
            var options = new MetadataOptions
            {
                ProvidarrBaseUrl = "https://providarr.example"
            };
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.RadarrMetadata.Create()
                .SetSegment("route", "movie")
                .Resource("535167")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.example/radarr/v1/movie/535167");
        }

        [Test]
        public void should_honour_explicit_movie_url_override()
        {
            var options = new MetadataOptions { MovieUrl = "https://custom.example/v1/{route}" };
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.RadarrMetadata.Create()
                .SetSegment("route", "movie")
                .Resource("535167")
                .Build();

            request.Url.FullUri.Should().Be("https://custom.example/v1/movie/535167");
        }

        [Test]
        public void should_default_series_metadata_to_the_public_providarr()
        {
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(new MetadataOptions()));

            var request = builder.TvdbMetadata.Create()
                .SetSegment("route", "shows")
                .Resource("327417")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.deprived.dev/sonarr/v1/tvdb/shows/en/327417");
        }

        [Test]
        public void should_point_series_metadata_at_a_custom_providarr()
        {
            var options = new MetadataOptions { ProvidarrBaseUrl = "https://providarr.example/" };
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.TvdbMetadata.Create()
                .SetSegment("route", "shows")
                .Resource("327417")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.example/sonarr/v1/tvdb/shows/en/327417");
        }

        [Test]
        public void should_use_builtin_tmdb_token_by_default()
        {
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(new MetadataOptions()));

            builder.AuthToken.Should().StartWith("eyJ");
        }

        [Test]
        public void should_honour_tmdb_api_key_override()
        {
            var options = new MetadataOptions { TmdbApiKey = "  my-own-tmdb-key  " };
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            builder.AuthToken.Should().Be("my-own-tmdb-key");
        }

        [Test]
        public void should_default_services_to_the_public_providarr()
        {
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(new MetadataOptions()));

            var request = builder.Services.Create()
                .Resource("/scenemapping")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.deprived.dev/sonarr/services/scenemapping");
        }

        [Test]
        public void should_point_services_at_a_custom_providarr()
        {
            var options = new MetadataOptions { ProvidarrBaseUrl = "https://providarr.example" };
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.Services.Create()
                .Resource("/dailyseries")
                .Build();

            request.Url.FullUri.Should().Be("https://providarr.example/sonarr/services/dailyseries");
        }

        [Test]
        public void should_honour_explicit_services_url_override()
        {
            var options = new MetadataOptions { ServicesUrl = "https://custom.example/services/" };
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.Services.Create()
                .Resource("/time")
                .Build();

            request.Url.FullUri.Should().Be("https://custom.example/services/time");
        }

        [Test]
        public void should_force_retry_for_the_default_providarr_host()
        {
            var builder = new RadarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(new MetadataOptions()));

            var request = builder.RadarrMetadata.Create()
                .SetSegment("route", "movie")
                .Resource("1")
                .Build();

            request.RetryPolicy.Should().NotBeNull();
            request.RetryPolicy.Enabled.Should().BeTrue();
        }

        [Test]
        public void should_not_retry_alternative_hosts_by_default()
        {
            var options = new MetadataOptions { ProvidarrBaseUrl = "https://providarr.internal" };
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.TvdbMetadata.Create()
                .SetSegment("route", "shows")
                .Resource("1")
                .Build();

            request.RetryPolicy.Should().BeNull();
        }

        [Test]
        public void should_retry_alternative_hosts_when_opted_in()
        {
            var options = new MetadataOptions
            {
                ProvidarrBaseUrl = "https://providarr.internal",
                RetryAlternativeProviders = true
            };
            var builder = new SonarrCloudRequestBuilder(Microsoft.Extensions.Options.Options.Create(options));

            var request = builder.TvdbMetadata.Create()
                .SetSegment("route", "shows")
                .Resource("1")
                .Build();

            request.RetryPolicy.Should().NotBeNull();
        }
    }
}
