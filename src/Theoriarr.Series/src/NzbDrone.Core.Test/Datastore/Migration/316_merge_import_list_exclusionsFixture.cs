using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class merge_import_list_exclusionsFixture : MigrationTest<merge_import_list_exclusions>
    {
        [Test]
        public void should_add_movie_columns_to_import_list_exclusions()
        {
            var db = WithMigrationTestDb();

            // Selecting the columns fails if the migration did not add them.
            db.Query<ExclusionModel>("SELECT \"MediaType\", \"TmdbId\", \"MovieYear\" FROM \"ImportListExclusions\"").Should().BeEmpty();
        }

        [Test]
        public void should_keep_series_rows_and_backfill_movie_rows()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("ImportListExclusions").Row(new { TvdbId = 81189, Title = "Breaking Bad" });
                c.Insert.IntoTable("ImportExclusions").Row(new { TmdbId = 27205, MovieTitle = "Inception", MovieYear = 2010 });
            });

            var rows = db.Query<ExclusionModel>("SELECT * FROM \"ImportListExclusions\" ORDER BY \"Id\"").ToList();

            rows.Should().HaveCount(2);

            rows[0].MediaType.Should().Be(0, "existing series rows stay Series");
            rows[0].TvdbId.Should().Be(81189);
            rows[0].Title.Should().Be("Breaking Bad");

            rows[1].MediaType.Should().Be(1, "movie rows are tagged Movie");
            rows[1].TmdbId.Should().Be(27205);
            rows[1].Title.Should().Be("Inception");
            rows[1].MovieYear.Should().Be(2010);
        }

        [Test]
        public void should_backfill_multiple_movie_rows()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("ImportExclusions").Row(new { TmdbId = 27205, MovieTitle = "Inception", MovieYear = 2010 });
                c.Insert.IntoTable("ImportExclusions").Row(new { TmdbId = 550, MovieTitle = "Fight Club", MovieYear = 1999 });
            });

            var rows = db.Query<ExclusionModel>("SELECT * FROM \"ImportListExclusions\" ORDER BY \"TmdbId\"").ToList();

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.MediaType == 1);
        }

        private class ExclusionModel
        {
            public int Id { get; set; }
            public int MediaType { get; set; }
            public int TvdbId { get; set; }
            public int TmdbId { get; set; }
            public string Title { get; set; }
            public int MovieYear { get; set; }
        }
    }
}
