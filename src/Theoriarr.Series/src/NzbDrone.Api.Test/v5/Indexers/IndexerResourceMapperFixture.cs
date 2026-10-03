using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Localization;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Indexers;
using Sonarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v5.Indexers
{
    [TestFixture]
    public class IndexerResourceMapperFixture : TestBase
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns<string, Dictionary<string, object>>((s, d) => s);

            SchemaBuilder.Initialize(Mocker.Container);
        }

        private static IndexerResource BuildResource(NewznabSettings settings, params string[] omittedFields)
        {
            var fields = SchemaBuilder.ToSchema(settings);

            if (omittedFields.Length > 0)
            {
                fields = fields.Where(f => !omittedFields.Contains(f.Name)).ToList();
            }

            var resource = new IndexerResource
            {
                Name = "Test",
                Implementation = "Newznab",
                ImplementationName = "Newznab",
                ConfigContract = "NewznabSettings",
                Fields = fields
            };

            // Round-trip through JSON so field values become JsonElements exactly as an API request.
            return STJson.Deserialize<IndexerResource>(STJson.ToJson(resource));
        }

        private static IEnumerable<int> CategoriesOf(IndexerResource resource)
        {
            return (IEnumerable<int>)resource.Fields.Single(f => f.Name == "categories").Value;
        }

        [Test]
        public void should_not_persist_derived_movie_categories_for_radarr_shaped_write()
        {
            var settings = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 2040 },
                MovieCategories = Array.Empty<int>()
            };

            var mapper = new IndexerResourceMapper();
            var model = mapper.ToModel(BuildResource(settings, "movieCategories"), null);

            var newznab = (NewznabSettings)model.Settings;
            newznab.Categories.Should().BeEquivalentTo(new[] { 2040 });
            newznab.MovieCategories.Should().BeEmpty();
            newznab.GetMovieCategories().Should().BeEquivalentTo(new[] { 2040 });

            var resource = mapper.ToResource(model);
            CategoriesOf(resource).Should().BeEquivalentTo(new[] { 2040 });
        }

        [Test]
        public void should_round_trip_sonarr_shaped_categories_without_acquiring_movie_categories()
        {
            var settings = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                MovieCategories = Array.Empty<int>()
            };

            var mapper = new IndexerResourceMapper();
            var model = mapper.ToModel(BuildResource(settings, "movieCategories"), null);

            var newznab = (NewznabSettings)model.Settings;
            newznab.Categories.Should().BeEquivalentTo(new[] { 5030, 5040 });
            newznab.MovieCategories.Should().BeEmpty();

            // Not persisted, but movie searches fall back to the standard Radarr categories.
            newznab.GetMovieCategories().Should().BeEquivalentTo(NewznabSettings.DefaultMovieCategories);

            var resource = mapper.ToResource(model);
            CategoriesOf(resource).Should().BeEquivalentTo(new[] { 5030, 5040 });
        }

        [Test]
        public void should_keep_movie_categories_when_a_series_shaped_write_omits_the_2xxx_categories()
        {
            var mapper = new IndexerResourceMapper();

            var radarrShaped = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 2040 },
                MovieCategories = Array.Empty<int>()
            };

            var firstModel = mapper.ToModel(BuildResource(radarrShaped, "movieCategories"), null);
            var firstNewznab = (NewznabSettings)firstModel.Settings;
            firstNewznab.MovieCategories.Should().BeEmpty();
            firstNewznab.GetMovieCategories().Should().BeEquivalentTo(new[] { 2040 });

            // Prowlarr's Sonarr client sends the shared list without any 2xxx; the movie range the
            // Radarr client stored must survive so movie searches keep working.
            var tvOnly = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                MovieCategories = Array.Empty<int>()
            };

            var secondModel = mapper.ToModel(BuildResource(tvOnly, "movieCategories"), firstModel);
            var secondNewznab = (NewznabSettings)secondModel.Settings;
            secondNewznab.MovieCategories.Should().BeEmpty();
            secondNewznab.GetTvCategories().Should().BeEquivalentTo(new[] { 5030, 5040 });
            secondNewznab.GetMovieCategories().Should().BeEquivalentTo(new[] { 2040 });
        }

        [Test]
        public void should_keep_series_categories_when_a_prowlarr_radarr_write_sends_only_the_shared_list()
        {
            var mapper = new IndexerResourceMapper();

            var sonarrShaped = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                MovieCategories = Array.Empty<int>()
            };

            var firstModel = mapper.ToModel(BuildResource(sonarrShaped, "movieCategories"), null);

            // Prowlarr's Radarr client sends neither animeCategories nor movieCategories, only the
            // shared `categories` list holding the 2xxx range.
            var radarrOnly = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 2040 },
                MovieCategories = Array.Empty<int>()
            };

            var secondModel = mapper.ToModel(BuildResource(radarrOnly, "movieCategories", "animeCategories"), firstModel);
            var secondNewznab = (NewznabSettings)secondModel.Settings;
            secondNewznab.GetTvCategories().Should().BeEquivalentTo(new[] { 5030, 5040 });
            secondNewznab.GetMovieCategories().Should().BeEquivalentTo(new[] { 2040 });
        }

        [Test]
        public void should_preserve_explicit_movie_categories_from_the_ui()
        {
            var settings = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                MovieCategories = new[] { 2000, 2010 }
            };

            var mapper = new IndexerResourceMapper();
            var model = mapper.ToModel(BuildResource(settings), null);

            var newznab = (NewznabSettings)model.Settings;
            newznab.Categories.Should().BeEquivalentTo(new[] { 5030, 5040 });
            newznab.MovieCategories.Should().BeEquivalentTo(new[] { 2000, 2010 });
            newznab.GetMovieCategories().Should().BeEquivalentTo(new[] { 2000, 2010 });
        }

        [Test]
        public void should_preserve_fields_scoped_away_from_the_request()
        {
            var existing = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                AnimeCategories = new[] { 5070 },
                AnimeStandardFormatSearch = true,
                MovieCategories = new[] { 2000, 2010 }
            };

            var existingDefinition = new IndexerDefinition
            {
                Id = 1,
                Name = "Test",
                Implementation = "Newznab",
                ConfigContract = "NewznabSettings",
                Settings = existing
            };

            var mapper = new IndexerResourceMapper();

            // A series-shaped save omits movieCategories (scoped away by ProviderFieldScoping).
            var seriesModel = (NewznabSettings)mapper.ToModel(BuildResource(existing, "movieCategories"), existingDefinition).Settings;
            seriesModel.AnimeCategories.Should().BeEquivalentTo(new[] { 5070 });
            seriesModel.AnimeStandardFormatSearch.Should().BeTrue();
            seriesModel.MovieCategories.Should().BeEquivalentTo(new[] { 2000, 2010 });

            // A movie-shaped save omits animeCategories/animeStandardFormatSearch.
            var movieModel = (NewznabSettings)mapper.ToModel(BuildResource(existing, "animeCategories", "animeStandardFormatSearch", "movieCategories"), existingDefinition).Settings;
            movieModel.AnimeCategories.Should().BeEquivalentTo(new[] { 5070 });
            movieModel.AnimeStandardFormatSearch.Should().BeTrue();
            movieModel.MovieCategories.Should().BeEquivalentTo(new[] { 2000, 2010 });
        }
    }
}
