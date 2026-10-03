using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(319)]
    public class add_transcode_profiles : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("TranscodeProfiles").Exists())
            {
                Create.TableForModel("TranscodeProfiles")
                    .WithColumn("Name").AsString(200).NotNullable()
                    .WithColumn("Codec").AsString(20).Nullable()
                    .WithColumn("Mode").AsInt32().NotNullable().WithDefaultValue(1)
                    .WithColumn("QualityValue").AsInt32().NotNullable().WithDefaultValue(0)
                    .WithColumn("Preset").AsString(50).Nullable()
                    .WithColumn("TargetSizeMB").AsInt32().Nullable()
                    .WithColumn("TargetPercent").AsInt32().Nullable()
                    .WithColumn("DeviceId").AsString(200).Nullable()
                    .WithColumn("Container").AsString(10).Nullable()
                    .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false);

                Seed();
            }

            if (Schema.Table("TranscodeJobs").Exists() && !Schema.Table("TranscodeJobs").Column("ProfileId").Exists())
            {
                Alter.Table("TranscodeJobs")
                     .AddColumn("ProfileId").AsInt32().Nullable()
                     .AddColumn("Container").AsString(10).Nullable();
            }
        }

        private void Seed()
        {
            Insert.IntoTable("TranscodeProfiles").Row(new
            {
                Name = "Efficient HEVC",
                Codec = "hevc",
                Mode = 1,
                QualityValue = 22,
                Preset = "medium",
                TargetSizeMB = (int?)null,
                TargetPercent = (int?)null,
                DeviceId = (string)null,
                Container = (string)null,
                IsDefault = true
            });

            Insert.IntoTable("TranscodeProfiles").Row(new
            {
                Name = "Compatible H.264",
                Codec = "h264",
                Mode = 1,
                QualityValue = 20,
                Preset = "medium",
                TargetSizeMB = (int?)null,
                TargetPercent = (int?)null,
                DeviceId = (string)null,
                Container = (string)null,
                IsDefault = false
            });

            Insert.IntoTable("TranscodeProfiles").Row(new
            {
                Name = "Smaller file (50%)",
                Codec = "hevc",
                Mode = 2,
                QualityValue = 0,
                Preset = "medium",
                TargetSizeMB = (int?)null,
                TargetPercent = 50,
                DeviceId = (string)null,
                Container = (string)null,
                IsDefault = false
            });

            Insert.IntoTable("TranscodeProfiles").Row(new
            {
                Name = "Remux to MKV",
                Codec = (string)null,
                Mode = 3,
                QualityValue = 0,
                Preset = (string)null,
                TargetSizeMB = (int?)null,
                TargetPercent = (int?)null,
                DeviceId = (string)null,
                Container = "mkv",
                IsDefault = false
            });
        }
    }
}
