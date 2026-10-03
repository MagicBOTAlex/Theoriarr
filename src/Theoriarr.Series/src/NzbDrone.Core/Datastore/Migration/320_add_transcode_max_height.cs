using System.Data;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(320)]
    public class add_transcode_max_height : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("TranscodeProfiles").Exists() && !Schema.Table("TranscodeProfiles").Column("MaxHeight").Exists())
            {
                Alter.Table("TranscodeProfiles").AddColumn("MaxHeight").AsInt32().Nullable();
            }

            if (Schema.Table("TranscodeJobs").Exists() && !Schema.Table("TranscodeJobs").Column("MaxHeight").Exists())
            {
                Alter.Table("TranscodeJobs").AddColumn("MaxHeight").AsInt32().Nullable();
            }

            if (Schema.Table("TranscodeProfiles").Exists())
            {
                Execute.WithConnection(SeedDownscaleProfiles);
            }
        }

        private void SeedDownscaleProfiles(IDbConnection conn, IDbTransaction tran)
        {
            Seed(conn, tran, "Downscale to 1440p", 1440);
            Seed(conn, tran, "Downscale to 1080p", 1080);
        }

        private void Seed(IDbConnection conn, IDbTransaction tran, string name, int maxHeight)
        {
            var exists = conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM \"TranscodeProfiles\" WHERE \"Name\" = @Name",
                new { Name = name },
                tran);

            if (exists > 0)
            {
                return;
            }

            conn.Execute(
                "INSERT INTO \"TranscodeProfiles\" (\"Name\", \"Codec\", \"Mode\", \"QualityValue\", \"Preset\", \"TargetSizeMB\", \"TargetPercent\", \"MaxHeight\", \"DeviceId\", \"Container\", \"IsDefault\") " +
                "VALUES (@Name, @Codec, @Mode, @QualityValue, @Preset, NULL, NULL, @MaxHeight, NULL, NULL, @IsDefault)",
                new
                {
                    Name = name,
                    Codec = "hevc",
                    Mode = 1,
                    QualityValue = 22,
                    Preset = "medium",
                    MaxHeight = maxHeight,
                    IsDefault = false
                },
                tran);
        }
    }
}
