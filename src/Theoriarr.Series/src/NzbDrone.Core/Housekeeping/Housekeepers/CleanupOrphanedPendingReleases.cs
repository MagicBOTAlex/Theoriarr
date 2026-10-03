using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedPendingReleases : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedPendingReleases(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""PendingReleases""
                                     WHERE ""Id"" IN (
                                     SELECT ""PendingReleases"".""Id"" FROM ""PendingReleases""
                                     LEFT OUTER JOIN ""Series""
                                     ON ""PendingReleases"".""SeriesId"" = ""Series"".""Id""
                                     LEFT OUTER JOIN ""Movies""
                                     ON ""PendingReleases"".""MovieId"" = ""Movies"".""Id""
                                     WHERE (""PendingReleases"".""SeriesId"" > 0 AND ""Series"".""Id"" IS NULL)
                                        OR (""PendingReleases"".""SeriesId"" = 0 AND ""PendingReleases"".""MovieId"" > 0 AND ""Movies"".""Id"" IS NULL)
                                        OR (""PendingReleases"".""SeriesId"" = 0 AND ""PendingReleases"".""MovieId"" = 0))");
        }
    }
}
