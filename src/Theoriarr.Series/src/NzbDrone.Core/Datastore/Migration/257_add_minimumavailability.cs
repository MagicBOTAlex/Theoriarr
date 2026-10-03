using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(257)]
    public class add_minimumavailability : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("ImportLists").Column("MinimumAvailability").Exists())
            {
                Alter.Table("ImportLists").AddColumn("MinimumAvailability").AsInt32().WithDefaultValue(3);
            }

            if (!Schema.Table("Movies").Column("MinimumAvailability").Exists())
            {
                Alter.Table("Movies").AddColumn("MinimumAvailability").AsInt32().WithDefaultValue(3);
            }
        }
    }
}
