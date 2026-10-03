using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.ImportLists.Exclusions
{
    public interface IImportListExclusionRepository : IBasicRepository<ImportListExclusion>
    {
        ImportListExclusion FindByTvdbId(int tvdbId);
        bool IsMovieExcluded(int tmdbId);
        ImportListExclusion FindByTmdbid(int tmdbId);
        List<int> AllExcludedTmdbIds();
    }

    public class ImportListExclusionRepository : BasicRepository<ImportListExclusion>, IImportListExclusionRepository
    {
        public ImportListExclusionRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public ImportListExclusion FindByTvdbId(int tvdbId)
        {
            return Query(m => m.TvdbId == tvdbId).SingleOrDefault();
        }

        public bool IsMovieExcluded(int tmdbId)
        {
            return Query(x => x.TmdbId == tmdbId).Any();
        }

        public ImportListExclusion FindByTmdbid(int tmdbId)
        {
            return Query(x => x.TmdbId == tmdbId).SingleOrDefault();
        }

        public List<int> AllExcludedTmdbIds()
        {
            using var conn = _database.OpenConnection();

            return conn.Query<int>("SELECT \"TmdbId\" FROM \"ImportListExclusions\" WHERE \"MediaType\" = 1").ToList();
        }
    }
}
