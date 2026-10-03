using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class add_import_list_indexesFixture : MigrationTest<add_import_list_indexes>
    {
        [Test]
        public void should_create_import_list_items_import_list_id_index()
        {
            var db = WithMigrationTestDb();

            db.QueryScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_ImportListItems_ImportListId'")
              .Should().Be(1);
        }

        [Test]
        public void should_create_import_list_movies_list_id_index()
        {
            var db = WithMigrationTestDb();

            db.QueryScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_ImportListMovies_ListId'")
              .Should().Be(1);
        }
    }
}
