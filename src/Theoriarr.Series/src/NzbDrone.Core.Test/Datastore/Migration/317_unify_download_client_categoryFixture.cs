using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class unify_download_client_categoryFixture : MigrationTest<unify_download_client_category>
    {
        [Test]
        public void should_not_fail_if_no_download_clients()
        {
            var db = WithMigrationTestDb();

            var downloadClients = db.Query<DownloadClientDefinition317>("SELECT \"Settings\" FROM \"DownloadClients\"");

            downloadClients.Should().BeEmpty();
        }

        [Test]
        public void should_clear_legacy_radarr_movie_category()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("DownloadClients").Row(new
                {
                    Enable = true,
                    Name = "qBit",
                    Implementation = "QBittorrent",
                    Settings = new { TvCategory = "theoriarr", MovieCategory = "radarr" }.ToJson(),
                    ConfigContract = "QBittorrentSettings"
                });
            });

            var settings = Settings(db);

            settings["movieCategory"].Value<string>().Should().BeEmpty();
            settings["tvCategory"].Value<string>().Should().Be("theoriarr");
        }

        [Test]
        public void should_clear_legacy_capitalised_movies_category()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("DownloadClients").Row(new
                {
                    Enable = true,
                    Name = "nzbget",
                    Implementation = "Nzbget",
                    Settings = new { TvCategory = "tv", MovieCategory = "Movies" }.ToJson(),
                    ConfigContract = "NzbgetSettings"
                });
            });

            Settings(db)["movieCategory"].Value<string>().Should().BeEmpty();
        }

        [Test]
        public void should_leave_a_custom_movie_category_untouched()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("DownloadClients").Row(new
                {
                    Enable = true,
                    Name = "qBit",
                    Implementation = "QBittorrent",
                    Settings = new { TvCategory = "theoriarr", MovieCategory = "uhd-movies" }.ToJson(),
                    ConfigContract = "QBittorrentSettings"
                });
            });

            Settings(db)["movieCategory"].Value<string>().Should().Be("uhd-movies");
        }

        private static JObject Settings(IDirectDataMapper db)
        {
            var downloadClients = db.Query<DownloadClientDefinition317>("SELECT \"Settings\" FROM \"DownloadClients\"");

            downloadClients.Should().HaveCount(1);

            return downloadClients.First().Settings;
        }

        private class DownloadClientDefinition317
        {
            public int Id { get; set; }
            public JObject Settings { get; set; }
        }
    }
}
