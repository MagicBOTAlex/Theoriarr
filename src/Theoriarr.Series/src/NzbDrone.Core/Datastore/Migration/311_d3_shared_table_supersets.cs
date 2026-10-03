using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // D3 — shared-table superset schema.
    //
    // History, Blocklist, QualityDefinitions, QualityProfiles, ExtraFiles, SubtitleFiles
    // and MetadataFiles become supersets: the SERIES columns stay in place, the MOVIES
    // columns are added (History/Blocklist already carry their movie keys from 236/252/
    // 265), and a MediaType discriminator records which domain wrote a row.
    //
    // Placement: 311 is appended after the renumbered MOVIES segment (235-310), which is
    // safe because no migration in that segment reads a column added here. The one early
    // prerequisite (QualityProfiles.Language, read by 254/269/280) is added ahead of the
    // segment in 234_movies_base_schema. See exec3-d3.md.
    [Migration(311)]
    public class d3_shared_table_supersets : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // --- History -------------------------------------------------------------
            // MediaType discriminator + nullable domain keys. The domain key columns get a
            // 0 default so the opposite domain's inserts (which omit them) still read back
            // as 0 for the non-nullable int properties on EpisodeHistory/MovieHistory.
            if (!Schema.Table("History").Column("MediaType").Exists())
            {
                Alter.Table("History").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            if (Schema.Table("History").Column("SeriesId").Exists())
            {
                Alter.Table("History").AlterColumn("SeriesId").AsInt32().Nullable().WithDefaultValue(0);
            }

            if (Schema.Table("History").Column("EpisodeId").Exists())
            {
                Alter.Table("History").AlterColumn("EpisodeId").AsInt32().Nullable().WithDefaultValue(0);
            }

            if (Schema.Table("History").Column("MovieId").Exists())
            {
                Alter.Table("History").AlterColumn("MovieId").AsInt32().Nullable().WithDefaultValue(0);
            }

            Execute.Sql("UPDATE \"History\" SET \"MediaType\" = 1 WHERE \"MediaType\" = 0 AND \"MovieId\" > 0");

            // --- Blocklist -----------------------------------------------------------
            // 252 already made SeriesId/EpisodeIds nullable and added MovieId; add the
            // discriminator and give SeriesId a 0 default for the same read-safety reason.
            if (!Schema.Table("Blocklist").Column("MediaType").Exists())
            {
                Alter.Table("Blocklist").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            if (!Schema.Table("Blocklist").Column("MovieId").Exists())
            {
                Alter.Table("Blocklist").AddColumn("MovieId").AsInt32().Nullable().WithDefaultValue(0);
            }

            if (Schema.Table("Blocklist").Column("SeriesId").Exists())
            {
                Alter.Table("Blocklist").AlterColumn("SeriesId").AsInt32().Nullable().WithDefaultValue(0);
            }

            Execute.Sql("UPDATE \"Blocklist\" SET \"MediaType\" = 1 WHERE \"MediaType\" = 0 AND \"MovieId\" > 0");

            // --- QualityDefinitions --------------------------------------------------
            // SERIES 207 dropped the per-definition sizes (moved to profile items). They
            // are re-added as the MOVIES domain columns plus the D3 discriminator.
            if (!Schema.Table("QualityDefinitions").Column("MinSize").Exists())
            {
                Alter.Table("QualityDefinitions").AddColumn("MinSize").AsDouble().Nullable();
            }

            if (!Schema.Table("QualityDefinitions").Column("MaxSize").Exists())
            {
                Alter.Table("QualityDefinitions").AddColumn("MaxSize").AsDouble().Nullable();
            }

            if (!Schema.Table("QualityDefinitions").Column("PreferredSize").Exists())
            {
                Alter.Table("QualityDefinitions").AddColumn("PreferredSize").AsDouble().Nullable();
            }

            if (!Schema.Table("QualityDefinitions").Column("MediaType").Exists())
            {
                Alter.Table("QualityDefinitions").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            // --- QualityProfiles -----------------------------------------------------
            // Language is the MOVIES profile-level domain column (added as a prerequisite
            // in 234 so 254/269/280 can read it). Add the D3 discriminator here.
            if (!Schema.Table("QualityProfiles").Column("MediaType").Exists())
            {
                Alter.Table("QualityProfiles").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            // --- ImportLists ---------------------------------------------------------
            // D5 already models ImportListDefinition.MediaType; back the column so the
            // merged import-list pipeline can round-trip the discriminator.
            if (!Schema.Table("ImportLists").Column("MediaType").Exists())
            {
                Alter.Table("ImportLists").AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            // ImportListDefinition also maps the MOVIES "Enabled"/"EnableAuto" names, but
            // the SERIES table only has EnableAutomaticAdd. 295_collections selects them,
            // so add and backfill them from the SERIES column.
            if (!Schema.Table("ImportLists").Column("Enabled").Exists())
            {
                Alter.Table("ImportLists").AddColumn("Enabled").AsBoolean().NotNullable().WithDefaultValue(false);
                Execute.Sql("UPDATE \"ImportLists\" SET \"Enabled\" = COALESCE(\"EnableAutomaticAdd\", 0)");
            }

            if (!Schema.Table("ImportLists").Column("EnableAuto").Exists())
            {
                Alter.Table("ImportLists").AddColumn("EnableAuto").AsBoolean().NotNullable().WithDefaultValue(false);
                Execute.Sql("UPDATE \"ImportLists\" SET \"EnableAuto\" = COALESCE(\"EnableAutomaticAdd\", 0)");
            }

            // --- ExtraFiles / SubtitleFiles / MetadataFiles --------------------------
            AddExtraFileSuperset("ExtraFiles");
            AddExtraFileSuperset("SubtitleFiles");
            AddExtraFileSuperset("MetadataFiles");

            // Remap MOVIES metadata types to the D7 union (1->6 MovieMetadata,
            // 2->7 MovieImage). Only rows written by the movie pipeline (MovieId set)
            // are touched, so SERIES 1-5 rows are preserved.
            if (Schema.Table("MetadataFiles").Column("MovieId").Exists())
            {
                Execute.Sql("UPDATE \"MetadataFiles\" SET \"Type\" = \"Type\" + 5 WHERE \"MovieId\" IS NOT NULL AND \"Type\" IN (1, 2)");
            }
        }

        private void AddExtraFileSuperset(string table)
        {
            // The SERIES key columns were NOT NULL (099/039). Drop the constraint so MOVIES
            // rows (which only populate MovieId/MovieFileId from 265) can be stored. The CLR
            // ExtraFile properties are already int?.
            if (Schema.Table(table).Column("SeriesId").Exists())
            {
                Alter.Table(table).AlterColumn("SeriesId").AsInt32().Nullable();
            }

            if (Schema.Table(table).Column("EpisodeFileId").Exists())
            {
                Alter.Table(table).AlterColumn("EpisodeFileId").AsInt32().Nullable();
            }

            if (Schema.Table(table).Column("SeasonNumber").Exists())
            {
                Alter.Table(table).AlterColumn("SeasonNumber").AsInt32().Nullable();
            }

            if (!Schema.Table(table).Column("MediaType").Exists())
            {
                Alter.Table(table).AddColumn("MediaType").AsInt32().NotNullable().WithDefaultValue(0);
            }

            // Flag any pre-existing movie rows (MovieId populated by 265).
            if (Schema.Table(table).Column("MovieId").Exists())
            {
                Execute.Sql($"UPDATE \"{table}\" SET \"MediaType\" = 1 WHERE \"MediaType\" = 0 AND \"MovieId\" IS NOT NULL");
            }
        }
    }
}
