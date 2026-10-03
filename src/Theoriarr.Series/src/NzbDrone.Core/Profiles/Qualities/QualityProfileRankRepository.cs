using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Profiles.Qualities
{
    public interface IQualityProfileRankRepository : IBasicRepository<QualityProfileQualityRank>
    {
        void ReplaceForProfile(int profileId, IEnumerable<QualityProfileQualityRank> ranks);
        void DeleteForProfile(int profileId);
    }

    public class QualityProfileRankRepository : BasicRepository<QualityProfileQualityRank>, IQualityProfileRankRepository
    {
        public QualityProfileRankRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void ReplaceForProfile(int profileId, IEnumerable<QualityProfileQualityRank> ranks)
        {
            var toInsert = ranks.ToList();

            // Delete and insert must happen on the same connection/transaction so a
            // failure can't leave the profile without any quality ranks.
            using (var conn = _database.OpenConnection())
            using (var tran = conn.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                conn.Execute($"DELETE FROM \"{_table}\" WHERE \"ProfileId\" = @ProfileId", new { ProfileId = profileId }, tran);

                foreach (var rank in toInsert)
                {
                    Insert(conn, tran, rank);
                }

                tran.Commit();
            }
        }

        public void DeleteForProfile(int profileId)
        {
            Delete(r => r.ProfileId == profileId);
        }
    }
}
