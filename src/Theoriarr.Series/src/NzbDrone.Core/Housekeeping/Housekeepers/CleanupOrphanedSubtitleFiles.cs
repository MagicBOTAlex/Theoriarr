using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedSubtitleFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedSubtitleFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedBySeries();
            DeleteOrphanedByEpisodeFile();
            DeleteWhereEpisodeFileIsZero();
            DeleteOrphanedByMovie();
            DeleteOrphanedByMovieFile();
        }

        private void DeleteOrphanedBySeries()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""SubtitleFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""SubtitleFiles"".""Id"" FROM ""SubtitleFiles""
                                     LEFT OUTER JOIN ""Series""
                                     ON ""SubtitleFiles"".""SeriesId"" = ""Series"".""Id""
                                     WHERE ""Series"".""Id"" IS NULL
                                     AND ""SubtitleFiles"".""MediaType"" = 0)");
        }

        private void DeleteOrphanedByEpisodeFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""SubtitleFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""SubtitleFiles"".""Id"" FROM ""SubtitleFiles""
                                     LEFT OUTER JOIN ""EpisodeFiles""
                                     ON ""SubtitleFiles"".""EpisodeFileId"" = ""EpisodeFiles"".""Id""
                                     WHERE ""SubtitleFiles"".""EpisodeFileId"" > 0
                                     AND ""EpisodeFiles"".""Id"" IS NULL
                                     AND ""SubtitleFiles"".""MediaType"" = 0)");
        }

        private void DeleteWhereEpisodeFileIsZero()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""SubtitleFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""Id"" FROM ""SubtitleFiles""
                                     WHERE ""EpisodeFileId"" = 0
                                     AND ""MediaType"" = 0)");
        }

        private void DeleteOrphanedByMovie()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""SubtitleFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""SubtitleFiles"".""Id"" FROM ""SubtitleFiles""
                                     LEFT OUTER JOIN ""Movies""
                                     ON ""SubtitleFiles"".""MovieId"" = ""Movies"".""Id""
                                     WHERE ""Movies"".""Id"" IS NULL
                                     AND ""SubtitleFiles"".""MediaType"" = 1)");
        }

        private void DeleteOrphanedByMovieFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""SubtitleFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""SubtitleFiles"".""Id"" FROM ""SubtitleFiles""
                                     LEFT OUTER JOIN ""MovieFiles""
                                     ON ""SubtitleFiles"".""MovieFileId"" = ""MovieFiles"".""Id""
                                     WHERE ""SubtitleFiles"".""MovieFileId"" > 0
                                     AND ""MovieFiles"".""Id"" IS NULL
                                     AND ""SubtitleFiles"".""MediaType"" = 1)");
        }
    }
}
