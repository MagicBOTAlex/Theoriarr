using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Synthesized reconciliation migration (report 09, D9).
    //
    // The Radarr (MOVIES) tree squashed Sonarr ids 1-103 into `001_initial_setup`
    // plus an inert `002_103_sonarr_squashed` placeholder. That squashed setup
    // created the movie-domain objects that the SERIES base (ids 000-233) never
    // creates, above all the `Movies` table. Every kept MOVIES migration appended
    // after 233 assumes those objects exist, so they are recreated here on top of
    // the SERIES final schema before the appended segment (235-310) runs.
    //
    // Only movie-domain DDL is created. Shared tables (History, Blocklist,
    // QualityProfiles, Notifications, ...) are owned by the SERIES timeline and
    // are not replayed. `ImportListMovies` is created here because the MOVIES
    // migration that originally created it (`181_list_movies_table`) is dropped
    // as a duplicate of SERIES `142_import_lists`, yet `294_movie_metadata`
    // (MOVIES 207) and `295_collections` (MOVIES 208) depend upon it.
    [Migration(234)]
    public class movies_base_schema : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // MOVIES 001_initial_setup -> "Movies" (pre-207 state; movie metadata
            // columns are later moved into MovieMetadata by 294_movie_metadata).
            if (!Schema.Table("Movies").Exists())
            {
                Create.TableForModel("Movies")
                    .WithColumn("ImdbId").AsString().Unique()
                    .WithColumn("Title").AsString()
                    .WithColumn("TitleSlug").AsString().Unique()
                    .WithColumn("SortTitle").AsString().Nullable()
                    .WithColumn("CleanTitle").AsString()
                    .WithColumn("Status").AsInt32()
                    .WithColumn("Overview").AsString().Nullable()
                    .WithColumn("Images").AsString()
                    .WithColumn("Path").AsString()
                    .WithColumn("Monitored").AsBoolean()
                    .WithColumn("QualityProfileId").AsInt32()
                    .WithColumn("LastInfoSync").AsDateTime().Nullable()
                    .WithColumn("LastDiskSync").AsDateTime().Nullable()
                    .WithColumn("Runtime").AsInt32()
                    .WithColumn("InCinemas").AsDateTime().Nullable()
                    .WithColumn("Year").AsInt32().Nullable()
                    .WithColumn("Added").AsDateTime().Nullable()
                    .WithColumn("Actors").AsString().Nullable()
                    .WithColumn("Ratings").AsString().Nullable()
                    .WithColumn("Genres").AsString().Nullable()
                    .WithColumn("Tags").AsString().Nullable()
                    .WithColumn("Certification").AsString().Nullable()
                    .WithColumn("AddOptions").AsString().Nullable();
            }

            // MOVIES 181_list_movies_table -> "ImportListMovies" (that migration is
            // dropped because SERIES 142 already owns ImportLists/ImportListStatus).
            if (!Schema.Table("ImportListMovies").Exists())
            {
                Create.TableForModel("ImportListMovies")
                    .WithColumn("ImdbId").AsString().Nullable()
                    .WithColumn("TmdbId").AsInt32()
                    .WithColumn("ListId").AsInt32()
                    .WithColumn("Title").AsString()
                    .WithColumn("SortTitle").AsString().Nullable()
                    .WithColumn("Status").AsInt32()
                    .WithColumn("Overview").AsString().Nullable()
                    .WithColumn("Images").AsString()
                    .WithColumn("LastInfoSync").AsDateTime().Nullable()
                    .WithColumn("Runtime").AsInt32()
                    .WithColumn("InCinemas").AsDateTime().Nullable()
                    .WithColumn("Year").AsInt32().Nullable()
                    .WithColumn("Ratings").AsString().Nullable()
                    .WithColumn("Genres").AsString().Nullable()
                    .WithColumn("Certification").AsString().Nullable()
                    .WithColumn("Collection").AsString().Nullable()
                    .WithColumn("Website").AsString().Nullable()
                    .WithColumn("OriginalTitle").AsString().Nullable()
                    .WithColumn("PhysicalRelease").AsDateTime().Nullable()
                    .WithColumn("Translations").AsString()
                    .WithColumn("Studio").AsString().Nullable()
                    .WithColumn("YouTubeTrailerId").AsString().Nullable()
                    .WithColumn("DigitalRelease").AsDateTime().Nullable();
            }

            // MOVIES 181_list_movies_table also added SearchOnAdd to the list table.
            // SERIES ImportLists (142) predates that column, so add it here.
            if (!Schema.Table("ImportLists").Column("SearchOnAdd").Exists())
            {
                Alter.Table("ImportLists").AddColumn("SearchOnAdd").AsBoolean().WithDefaultValue(false);
            }

            // MOVIES ImportListDefinition uses Enabled/EnableAuto, which later movie
            // migrations (295_collections) read. SERIES only has EnableAutomaticAdd, so
            // add them here and backfill from it.
            if (!Schema.Table("ImportLists").Column("Enabled").Exists())
            {
                Alter.Table("ImportLists").AddColumn("Enabled").AsBoolean().WithDefaultValue(false);

                if (Schema.Table("ImportLists").Column("EnableAutomaticAdd").Exists())
                {
                    Execute.Sql("UPDATE \"ImportLists\" SET \"Enabled\" = COALESCE(\"EnableAutomaticAdd\", 0)");
                }
            }

            if (!Schema.Table("ImportLists").Column("EnableAuto").Exists())
            {
                Alter.Table("ImportLists").AddColumn("EnableAuto").AsBoolean().WithDefaultValue(false);

                if (Schema.Table("ImportLists").Column("EnableAutomaticAdd").Exists())
                {
                    Execute.Sql("UPDATE \"ImportLists\" SET \"EnableAuto\" = COALESCE(\"EnableAutomaticAdd\", 0)");
                }
            }

            // MOVIES 001_initial_setup carried PendingReleases.MovieId inline (the
            // ancestor never split it into a post-fork migration). SERIES 054 does
            // not have it.
            if (!Schema.Table("PendingReleases").Column("MovieId").Exists())
            {
                Alter.Table("PendingReleases").AddColumn("MovieId").AsInt32().WithDefaultValue(0);
            }

            // Report 09 C5: SERIES 139 created DownloadHistory.SeriesId NOT NULL and
            // MOVIES 172 (which would have added MovieId) is dropped, so the merged
            // polymorphic table is reconciled here. Exactly one side is populated.
            if (!Schema.Table("DownloadHistory").Column("MovieId").Exists())
            {
                Alter.Table("DownloadHistory").AddColumn("MovieId").AsInt32().Nullable();
                Execute.Sql("CREATE INDEX \"IX_DownloadHistory_MovieId\" ON \"DownloadHistory\" (\"MovieId\")");
            }

            if (Schema.Table("DownloadHistory").Column("SeriesId").Exists())
            {
                Alter.Table("DownloadHistory").AlterColumn("SeriesId").AsInt32().Nullable();
            }

            // D3 prerequisite: the appended MOVIES migrations read QualityProfiles.Language
            // (254 update_qualities_and_profiles, 269 add_language_to_file_history_blacklist,
            // 280 fix_invalid_profile_references) but the SERIES schema never carried the
            // movie profile-level language. There is no free migration id between 233 and
            // 234, so the prerequisite is added here, ahead of the appended segment. The
            // full D3 shared-table superset lives in 311_d3_shared_table_supersets.
            if (!Schema.Table("QualityProfiles").Column("Language").Exists())
            {
                Alter.Table("QualityProfiles").AddColumn("Language").AsInt32().Nullable().WithDefaultValue(1);
            }
        }
    }
}
