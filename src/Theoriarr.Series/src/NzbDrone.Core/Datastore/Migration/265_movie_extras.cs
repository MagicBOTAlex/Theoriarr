using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(265)]
    public class movie_extras : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Report 09 C3: ExtraFiles/SubtitleFiles/MetadataFiles are SHARED tables
            // owned by the SERIES timeline (099/170/198). MOVIES 142 originally did
            // Delete.Table on all three and recreated them with movie-only keys,
            // which would destroy every Sonarr subtitle/extra/metadata row. Add the
            // movie-domain keys additively instead; the Series keys stay in place and
            // both consumers tolerate the other side being NULL.
            AddMovieColumns("ExtraFiles");
            AddMovieColumns("SubtitleFiles");
            AddMovieColumns("MetadataFiles");
        }

        private void AddMovieColumns(string table)
        {
            if (!Schema.Table(table).Column("MovieId").Exists())
            {
                Alter.Table(table).AddColumn("MovieId").AsInt32().Nullable();
            }

            if (!Schema.Table(table).Column("MovieFileId").Exists())
            {
                Alter.Table(table).AddColumn("MovieFileId").AsInt32().Nullable();
            }
        }
    }
}
