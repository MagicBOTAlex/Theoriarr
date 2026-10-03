using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class new_list_serverFixture : MigrationTest<new_list_server>
    {
        [Test]
        public void should_not_throw_when_radarr_settings_have_no_api_url()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("ImportLists").Row(new
                {
                    Name = "missing-api-url",
                    Implementation = "RadarrListImport",
                    Settings = "{\"path\":\"/imdb/top250\"}",
                    ConfigContract = "RadarrListSettings",
                    RootFolderPath = "/movies",
                    ShouldMonitor = 0,
                    QualityProfileId = 1,
                    SeriesType = 0,
                    SeasonFolder = true,
                    SearchForMissingEpisodes = true,
                    MonitorNewItems = 0,
                    TagExisting = false,
                    MinimumAvailability = 3
                });
            });

            var rows = db.Query<ImportListModel>("SELECT \"Id\", \"ConfigContract\", \"Implementation\" FROM \"ImportLists\"").ToList();

            rows.Should().ContainSingle();
            rows[0].ConfigContract.Should().Be("RadarrListSettings");
        }

        [Test]
        public void should_convert_radarr_imdb_list()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("ImportLists").Row(new
                {
                    Name = "imdb-top250",
                    Implementation = "RadarrListImport",
                    Settings = "{\"apiUrl\":\"https://api.radarr.video/v2\",\"path\":\"/imdb/top250\"}",
                    ConfigContract = "RadarrListSettings",
                    RootFolderPath = "/movies",
                    ShouldMonitor = 0,
                    QualityProfileId = 1,
                    SeriesType = 0,
                    SeasonFolder = true,
                    SearchForMissingEpisodes = true,
                    MonitorNewItems = 0,
                    TagExisting = false,
                    MinimumAvailability = 3
                });
            });

            var rows = db.Query<ImportListModel>("SELECT \"Id\", \"ConfigContract\", \"Implementation\" FROM \"ImportLists\"").ToList();

            rows.Should().ContainSingle();
            rows[0].ConfigContract.Should().Be("IMDbListSettings");
            rows[0].Implementation.Should().Be("IMDbListImport");
        }

        [Test]
        public void should_persist_converted_stevenlu_lists_and_skip_unknown_links()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("ImportLists").Row(new
                {
                    Name = "known-stevenlu",
                    Implementation = "StevenLuImport",
                    Settings = "{\"link\":\"https://s3.amazonaws.com/popular-movies/movies.json\"}",
                    ConfigContract = "StevenLuSettings",
                    RootFolderPath = "/movies",
                    ShouldMonitor = 0,
                    QualityProfileId = 1,
                    SeriesType = 0,
                    SeasonFolder = true,
                    SearchForMissingEpisodes = true,
                    MonitorNewItems = 0,
                    TagExisting = false,
                    MinimumAvailability = 3
                });

                c.Insert.IntoTable("ImportLists").Row(new
                {
                    Name = "unknown-stevenlu",
                    Implementation = "StevenLuImport",
                    Settings = "{\"link\":\"https://s3.amazonaws.com/popular-movies/unknown\"}",
                    ConfigContract = "StevenLuSettings",
                    RootFolderPath = "/movies",
                    ShouldMonitor = 0,
                    QualityProfileId = 1,
                    SeriesType = 0,
                    SeasonFolder = true,
                    SearchForMissingEpisodes = true,
                    MonitorNewItems = 0,
                    TagExisting = false,
                    MinimumAvailability = 3
                });
            });

            var rows = db.Query<ImportListModel>("SELECT \"Id\", \"ConfigContract\", \"Implementation\" FROM \"ImportLists\" ORDER BY \"Id\"").ToList();

            rows.Should().HaveCount(2);
            rows[0].ConfigContract.Should().Be("StevenLu2Settings");
            rows[0].Implementation.Should().Be("StevenLu2Import");
            rows[1].ConfigContract.Should().Be("StevenLuSettings");
            rows[1].Implementation.Should().Be("StevenLuImport");
        }

        private class ImportListModel
        {
            public int Id { get; set; }
            public string ConfigContract { get; set; }
            public string Implementation { get; set; }
        }
    }
}
