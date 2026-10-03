using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // D6 — merge the two import-list exclusion stores into one MediaType-tagged superset.
    //
    // SERIES kept its exclusions in `ImportListExclusions` (TvdbId/Title, with a legacy
    // UNIQUE(TvdbId) constraint); MOVIES kept its in `ImportExclusions`
    // (TmdbId/MovieTitle/MovieYear). The series table becomes the single store: it gains the
    // movie columns plus a MediaType discriminator and the movie rows are copied over.
    //
    // The legacy UNIQUE(TvdbId) constraint must go: movie rows have TvdbId = 0, and there can be
    // many of them. SQLite (and Postgres) cannot drop an inline table constraint, so the table
    // is rebuilt via a temp table. The copy runs before the drop, so no data is lost.
    //
    // The legacy `ImportExclusions` table is intentionally left in place (untouched) so the
    // migration is additive; the movie repository no longer reads it.
    [Migration(316)]
    public class merge_import_list_exclusions : NzbDroneMigrationBase
    {
        private const string TempTable = "ImportListExclusions_316";

        protected override void MainDbUpgrade()
        {
            // Rebuild `ImportListExclusions` without the UNIQUE(TvdbId) constraint, adding the
            // movie columns and backfilling Movie rows from the legacy movies table.
            Create.TableForModel(TempTable)
                  .WithColumn("TvdbId").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("Title").AsString().NotNullable()
                  .WithColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("TmdbId").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("MovieYear").AsInt32().NotNullable().WithDefaultValue(0);

            // Existing series rows stay Series(0).
            Execute.Sql(
                "INSERT INTO \"ImportListExclusions_316\" (\"TvdbId\", \"Title\", \"MediaType\", \"TmdbId\", \"MovieYear\") " +
                "SELECT \"TvdbId\", \"Title\", 0, 0, 0 FROM \"ImportListExclusions\"");

            // Movie rows become Movie(1); the movie title moves into the shared `Title` column.
            if (Schema.Table("ImportExclusions").Exists())
            {
                Execute.Sql(
                    "INSERT INTO \"ImportListExclusions_316\" (\"TvdbId\", \"Title\", \"MediaType\", \"TmdbId\", \"MovieYear\") " +
                    "SELECT 0, COALESCE(\"MovieTitle\", ''), 1, \"TmdbId\", COALESCE(\"MovieYear\", 0) FROM \"ImportExclusions\"");
            }

            Delete.Table("ImportListExclusions");
            Rename.Table(TempTable).To("ImportListExclusions");
        }
    }
}
