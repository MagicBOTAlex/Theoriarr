using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(252)]
    public class add_movieid_to_blacklist : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Retarget to the SERIES canonical table name (renamed by SERIES 160).
            Alter.Table("Blocklist").AddColumn("MovieId").AsInt32().Nullable().WithDefaultValue(0);
            Alter.Table("Blocklist").AlterColumn("SeriesId").AsInt32().Nullable();
            Alter.Table("Blocklist").AlterColumn("EpisodeIds").AsString().Nullable();
        }
    }
}
