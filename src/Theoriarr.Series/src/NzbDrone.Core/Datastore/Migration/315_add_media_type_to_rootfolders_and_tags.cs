using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // D3 follow-up — media-type discriminators for the two shared stores that still had
    // none: RootFolders and Tags.
    //
    // Both tables were SERIES tables that the movie domain shares. Existing rows are
    // backfilled to 0 (MediaType.Series), which is also treated as the "shared/legacy"
    // value by SubsystemDomainScope: a 0 row stays visible to whichever domain actually
    // references it (and to both when unused), so no existing dropdown entry disappears.
    // Rows created through the API afterwards are stamped with the resolved subsystem
    // (movie key -> 1), which lets an explicitly movie-owned folder/tag stay out of the
    // series key's listing.
    [Migration(315)]
    public class add_media_type_to_rootfolders_and_tags : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // NOT NULL + default 0 backfills every pre-existing row in one step without a
            // separate UPDATE, preserving the current (shared) visibility.
            if (!Schema.Table("RootFolders").Column("MediaType").Exists())
            {
                Alter.Table("RootFolders").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            if (!Schema.Table("Tags").Column("MediaType").Exists())
            {
                Alter.Table("Tags").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }
        }
    }
}
