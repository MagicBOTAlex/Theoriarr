using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(322)]
    public class add_transcode_requeue : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("TranscodeJobs").Exists() && !Schema.Table("TranscodeJobs").Column("RequeuedJobId").Exists())
            {
                Alter.Table("TranscodeJobs").AddColumn("RequeuedJobId").AsInt32().Nullable();
            }
        }
    }
}
