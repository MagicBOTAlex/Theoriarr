using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(312)]
    public class recreate_quality_expression_indexes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // SQLite table rebuilds triggered by AlterColumn drop expression indexes
            // because the schema dumper cannot represent them. Recreate them so the
            // quality-id lookups added by 233 keep working after the movie migrations.
            IfDatabase(ProcessorIdConstants.SQLite).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_History_QualityId\" ON \"History\" (json_extract(\"Quality\", '$.quality'));");
            IfDatabase(ProcessorIdConstants.SQLite).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_EpisodeFiles_QualityId\" ON \"EpisodeFiles\" (json_extract(\"Quality\", '$.quality'));");
            IfDatabase(ProcessorIdConstants.SQLite).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Blocklist_QualityId\" ON \"Blocklist\" (json_extract(\"Quality\", '$.quality'));");

            IfDatabase(ProcessorIdConstants.PostgreSQL).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_History_QualityId\" ON \"History\" (((\"Quality\"::jsonb ->> 'quality')::int));");
            IfDatabase(ProcessorIdConstants.PostgreSQL).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_EpisodeFiles_QualityId\" ON \"EpisodeFiles\" (((\"Quality\"::jsonb ->> 'quality')::int));");
            IfDatabase(ProcessorIdConstants.PostgreSQL).Execute.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Blocklist_QualityId\" ON \"Blocklist\" (((\"Quality\"::jsonb ->> 'quality')::int));");
        }
    }
}
