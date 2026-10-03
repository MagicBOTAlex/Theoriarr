using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeJobRepository : IBasicRepository<TranscodeJob>
    {
        List<TranscodeJob> GetByStatus(TranscodeJobStatus status);
        List<TranscodeJob> GetActive();
        List<TranscodeJob> GetRecentIncludingActive(int limit);
        List<TranscodeJob> GetTerminal();
    }

    public class TranscodeJobRepository : BasicRepository<TranscodeJob>, ITranscodeJobRepository
    {
        public TranscodeJobRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<TranscodeJob> GetByStatus(TranscodeJobStatus status)
        {
            return Query(job => job.Status == status);
        }

        public List<TranscodeJob> GetActive()
        {
            return Query(job => job.Status == TranscodeJobStatus.Queued ||
                                job.Status == TranscodeJobStatus.Running ||
                                job.Status == TranscodeJobStatus.Transferring ||
                                job.Status == TranscodeJobStatus.AwaitingReview);
        }

        public List<TranscodeJob> GetRecentIncludingActive(int limit)
        {
            // The scheduler drains oldest-first, so a queue deeper than `limit` would hide the jobs
            // that are actually running behind newer terminal ones. Always surface every active job
            // and fill the remainder with the newest terminal jobs. Both halves are filtered, sorted
            // and bounded in SQL so a large history is not materialised on every poll.
            var active = Query(job => job.Status == TranscodeJobStatus.Queued ||
                                      job.Status == TranscodeJobStatus.Running ||
                                      job.Status == TranscodeJobStatus.Transferring ||
                                      job.Status == TranscodeJobStatus.AwaitingReview);

            var recentTerminal = Query(Builder()
                .Where<TranscodeJob>(job => job.Status != TranscodeJobStatus.Queued &&
                                            job.Status != TranscodeJobStatus.Running &&
                                            job.Status != TranscodeJobStatus.Transferring &&
                                            job.Status != TranscodeJobStatus.AwaitingReview)
                .OrderBy($"\"{_table}\".\"Id\" DESC LIMIT @limit", new { limit }));

            return MergeRecentIncludingActive(active, recentTerminal);
        }

        public List<TranscodeJob> GetTerminal()
        {
            return Query(job => job.Status == TranscodeJobStatus.Completed ||
                                job.Status == TranscodeJobStatus.Failed ||
                                job.Status == TranscodeJobStatus.Cancelled ||
                                job.Status == TranscodeJobStatus.Skipped);
        }

        // A job that finalises between the two reads above can appear in both halves; de-dup by id,
        // keeping the terminal (later) snapshot so the resource does not report a stale status.
        internal static List<TranscodeJob> MergeRecentIncludingActive(IEnumerable<TranscodeJob> active, IEnumerable<TranscodeJob> recentTerminal)
        {
            return active.Concat(recentTerminal)
                         .GroupBy(job => job.Id)
                         .Select(group => group.Last())
                         .OrderByDescending(job => job.Id)
                         .ToList();
        }
    }
}
