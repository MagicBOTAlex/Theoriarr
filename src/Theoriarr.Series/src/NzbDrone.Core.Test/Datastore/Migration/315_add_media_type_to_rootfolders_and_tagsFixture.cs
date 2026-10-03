using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class add_media_type_to_rootfolders_and_tagsFixture : MigrationTest<add_media_type_to_rootfolders_and_tags>
    {
        [Test]
        public void should_add_media_type_column_to_root_folders_and_tags()
        {
            var db = WithMigrationTestDb();

            // Selecting the column fails if the migration did not add it.
            db.Query<MediaTypeModel>("SELECT \"MediaType\" FROM \"RootFolders\"").Should().BeEmpty();
            db.Query<MediaTypeModel>("SELECT \"MediaType\" FROM \"Tags\"").Should().BeEmpty();
        }

        [Test]
        public void should_backfill_existing_rows_as_series_shared()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("RootFolders").Row(new { Path = "c:\\test\\tv" });
                c.Insert.IntoTable("Tags").Row(new { Label = "tv" });
            });

            var rootFolders = db.Query<MediaTypeModel>("SELECT \"MediaType\" FROM \"RootFolders\"");
            var tags = db.Query<MediaTypeModel>("SELECT \"MediaType\" FROM \"Tags\"");

            rootFolders.Should().HaveCount(1);
            rootFolders.Single().MediaType.Should().Be(0, "existing root folders must stay shared (Series = 0)");

            tags.Should().HaveCount(1);
            tags.Single().MediaType.Should().Be(0, "existing tags must stay shared (Series = 0)");
        }

        private class MediaTypeModel
        {
            public int MediaType { get; set; }
        }
    }
}
