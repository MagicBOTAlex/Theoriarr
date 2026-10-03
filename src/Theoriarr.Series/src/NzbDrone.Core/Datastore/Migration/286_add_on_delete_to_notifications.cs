using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(286)]
    public class add_movie_on_delete_to_notifications : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // MOVIES 182 (which created OnDelete) is dropped; add the movie columns
            // directly so the merged Notifications row serves both APIs (report 09 C6.3).
            if (!Schema.Table("Notifications").Column("OnMovieDelete").Exists())
            {
                Alter.Table("Notifications").AddColumn("OnMovieDelete").AsBoolean().WithDefaultValue(false);
            }

            if (!Schema.Table("Notifications").Column("OnMovieFileDelete").Exists())
            {
                Alter.Table("Notifications").AddColumn("OnMovieFileDelete").AsBoolean().WithDefaultValue(false);
            }

            if (!Schema.Table("Notifications").Column("OnMovieFileDeleteForUpgrade").Exists())
            {
                Alter.Table("Notifications").AddColumn("OnMovieFileDeleteForUpgrade").AsBoolean().WithDefaultValue(false);
            }
        }
    }
}
