using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(318)]
    public class add_transcode_jobs : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("TranscodeJobs").Exists())
            {
                return;
            }

            Create.TableForModel("TranscodeJobs")
                .WithColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("EpisodeFileId").AsInt32().Nullable()
                .WithColumn("MovieFileId").AsInt32().Nullable()
                .WithColumn("SeriesId").AsInt32().Nullable()
                .WithColumn("MovieId").AsInt32().Nullable()
                .WithColumn("SourcePath").AsString(2000).NotNullable()
                .WithColumn("OutputPath").AsString(2000).Nullable()
                .WithColumn("OriginalPath").AsString(2000).Nullable()
                .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("Mode").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("VideoCodec").AsString(50).Nullable()
                .WithColumn("RateControl").AsString(50).Nullable()
                .WithColumn("TargetSize").AsInt64().Nullable()
                .WithColumn("TargetPercent").AsInt32().Nullable()
                .WithColumn("QualityValue").AsInt32().Nullable()
                .WithColumn("Preset").AsString(50).Nullable()
                .WithColumn("DeviceId").AsString(200).Nullable()
                .WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("SourceSize").AsInt64().Nullable()
                .WithColumn("OutputSize").AsInt64().Nullable()
                .WithColumn("Progress").AsDouble().NotNullable().WithDefaultValue(0)
                .WithColumn("Speed").AsString(50).Nullable()
                .WithColumn("Fps").AsString(50).Nullable()
                .WithColumn("Eta").AsString(50).Nullable()
                .WithColumn("Error").AsString(4000).Nullable()
                .WithColumn("Message").AsString(1000).Nullable()
                .WithColumn("Trigger").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("StartedAt").AsDateTime().Nullable()
                .WithColumn("EndedAt").AsDateTime().Nullable()
                .WithColumn("LastUpdatedAt").AsDateTime().Nullable();
        }
    }
}
