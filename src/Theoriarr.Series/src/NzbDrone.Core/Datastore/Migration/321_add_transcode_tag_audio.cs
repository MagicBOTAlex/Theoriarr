using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(321)]
    public class add_transcode_tag_audio : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("TranscodeProfiles").Exists())
            {
                if (!Schema.Table("TranscodeProfiles").Column("Tag").Exists())
                {
                    Alter.Table("TranscodeProfiles").AddColumn("Tag").AsString(50).Nullable();
                }

                if (!Schema.Table("TranscodeProfiles").Column("PreferEnglishAudio").Exists())
                {
                    Alter.Table("TranscodeProfiles").AddColumn("PreferEnglishAudio").AsBoolean().NotNullable().WithDefaultValue(false);
                }

                // Give the existing profiles a sensible default tag so transcodes are visibly no
                // longer raw; the user can rename or clear it per profile.
                Execute.Sql("UPDATE \"TranscodeProfiles\" SET \"Tag\" = CASE WHEN \"Mode\" = 3 THEN 'Remux' ELSE 'Transcoded' END WHERE \"Tag\" IS NULL");
            }

            if (Schema.Table("TranscodeJobs").Exists())
            {
                if (!Schema.Table("TranscodeJobs").Column("Tag").Exists())
                {
                    Alter.Table("TranscodeJobs").AddColumn("Tag").AsString(50).Nullable();
                }

                if (!Schema.Table("TranscodeJobs").Column("PreferEnglishAudio").Exists())
                {
                    Alter.Table("TranscodeJobs").AddColumn("PreferEnglishAudio").AsBoolean().NotNullable().WithDefaultValue(false);
                }
            }
        }
    }
}
