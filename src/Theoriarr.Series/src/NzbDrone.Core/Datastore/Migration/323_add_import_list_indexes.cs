using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(323)]
    public class add_import_list_indexes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("ImportListItems").Index("IX_ImportListItems_ImportListId").Exists())
            {
                Create.Index("IX_ImportListItems_ImportListId")
                    .OnTable("ImportListItems")
                    .OnColumn("ImportListId");
            }

            if (!Schema.Table("ImportListMovies").Index("IX_ImportListMovies_ListId").Exists())
            {
                Create.Index("IX_ImportListMovies_ListId")
                    .OnTable("ImportListMovies")
                    .OnColumn("ListId");
            }
        }
    }
}
