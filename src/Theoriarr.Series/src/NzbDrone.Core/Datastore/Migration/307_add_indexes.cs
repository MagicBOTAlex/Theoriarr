using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(307)]
    public class add_movie_indexes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("Blocklist").Index("IX_Blocklist_MovieId").Exists())
            {
                Create.Index().OnTable("Blocklist").OnColumn("MovieId");
            }

            if (!Schema.Table("Blocklist").Index("IX_Blocklist_Date").Exists())
            {
                Create.Index().OnTable("Blocklist").OnColumn("Date");
            }

            if (!Schema.Table("History").Index("IX_History_MovieId_Date").Exists())
            {
                Create.Index()
                    .OnTable("History")
                    .OnColumn("MovieId").Ascending()
                    .OnColumn("Date").Descending();
            }

            if (Schema.Table("History").Index("IX_History_DownloadId").Exists())
            {
                Delete.Index().OnTable("History").OnColumn("DownloadId");
            }

            if (!Schema.Table("History").Index("IX_History_DownloadId_Date").Exists())
            {
                Create.Index()
                    .OnTable("History")
                    .OnColumn("DownloadId").Ascending()
                    .OnColumn("Date").Descending();
            }

            if (!Schema.Table("Movies").Index("IX_Movies_MovieFileId").Exists())
            {
                Create.Index().OnTable("Movies").OnColumn("MovieFileId");
            }

            if (!Schema.Table("Movies").Index("IX_Movies_Path").Exists())
            {
                Create.Index().OnTable("Movies").OnColumn("Path");
            }
        }
    }
}
