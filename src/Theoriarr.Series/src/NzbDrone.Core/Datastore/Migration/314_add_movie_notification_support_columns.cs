using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(314)]
    public class add_movie_notification_support_columns : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            AddBoolIfMissing("SupportsOnMovieAdded");
            AddBoolIfMissing("SupportsOnMovieDelete");
            AddBoolIfMissing("SupportsOnMovieFileDelete");
            AddBoolIfMissing("SupportsOnMovieFileDeleteForUpgrade");
        }

        private void AddBoolIfMissing(string column)
        {
            if (!Schema.Table("Notifications").Column(column).Exists())
            {
                Alter.Table("Notifications").AddColumn(column).AsBoolean().WithDefaultValue(false);
            }
        }
    }
}
