using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedMetadataFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMetadataFiles(IMainDatabase database)
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
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""Series""
                                     ON ""MetadataFiles"".""SeriesId"" = ""Series"".""Id""
                                     WHERE ""Series"".""Id"" IS NULL
                                     AND ""MetadataFiles"".""MediaType"" = 0)");
        }

        private void DeleteOrphanedByEpisodeFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""EpisodeFiles""
                                     ON ""MetadataFiles"".""EpisodeFileId"" = ""EpisodeFiles"".""Id""
                                     WHERE ""MetadataFiles"".""EpisodeFileId"" > 0
                                     AND ""EpisodeFiles"".""Id"" IS NULL
                                     AND ""MetadataFiles"".""MediaType"" = 0)");
        }

        private void DeleteWhereEpisodeFileIsZero()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""Id"" FROM ""MetadataFiles""
                                     WHERE ""Type"" IN (2, 5)
                                     AND ""EpisodeFileId"" = 0
                                     AND ""MediaType"" = 0)");
        }

        private void DeleteOrphanedByMovie()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""Movies""
                                     ON ""MetadataFiles"".""MovieId"" = ""Movies"".""Id""
                                     WHERE ""Movies"".""Id"" IS NULL
                                     AND ""MetadataFiles"".""MediaType"" = 1)");
        }

        private void DeleteOrphanedByMovieFile()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""MetadataFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""MetadataFiles"".""Id"" FROM ""MetadataFiles""
                                     LEFT OUTER JOIN ""MovieFiles""
                                     ON ""MetadataFiles"".""MovieFileId"" = ""MovieFiles"".""Id""
                                     WHERE ""MetadataFiles"".""MovieFileId"" > 0
                                     AND ""MovieFiles"".""Id"" IS NULL
                                     AND ""MetadataFiles"".""MediaType"" = 1)");
        }
    }
}
