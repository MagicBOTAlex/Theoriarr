using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.IndexerSearch
{
    public interface IReleaseSearchTracker
    {
        bool TryBegin(string searchId);
        CancellationToken GetToken(string searchId);
        void RecordIndexer(string searchId, List<DownloadDecision> decisions);
        void Complete(string searchId, List<DownloadDecision> decisions);
        void Cancel(string searchId);
        List<DownloadDecision> GetDecisions(string searchId);
    }

    // Holds the results of an in-flight interactive search so a client can pull the
    // results gathered so far while the remaining indexers keep searching. Entries are
    // pruned after a short expiry so abandoned searches do not leak.
    public class ReleaseSearchTracker : IReleaseSearchTracker
    {
        private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<string, TrackedSearch> _searches = new ConcurrentDictionary<string, TrackedSearch>(StringComparer.Ordinal);

        public bool TryBegin(string searchId)
        {
            Prune();

            return _searches.TryAdd(searchId, new TrackedSearch());
        }

        public CancellationToken GetToken(string searchId)
        {
            return _searches.TryGetValue(searchId, out var search) ? search.Cancellation.Token : CancellationToken.None;
        }

        public void Cancel(string searchId)
        {
            if (_searches.TryRemove(searchId, out var search))
            {
                search.Cancellation.Cancel();
            }
        }

        public void RecordIndexer(string searchId, List<DownloadDecision> decisions)
        {
            if (decisions == null || !_searches.TryGetValue(searchId, out var search))
            {
                return;
            }

            lock (search.Sync)
            {
                if (search.IsComplete)
                {
                    return;
                }

                search.Decisions.AddRange(decisions);
                search.LastTouchedUtc = DateTime.UtcNow;
            }
        }

        public void Complete(string searchId, List<DownloadDecision> decisions)
        {
            if (!_searches.TryGetValue(searchId, out var search))
            {
                return;
            }

            lock (search.Sync)
            {
                search.IsComplete = true;

                if (decisions != null)
                {
                    search.Decisions = decisions;
                }

                search.LastTouchedUtc = DateTime.UtcNow;
            }
        }

        public List<DownloadDecision> GetDecisions(string searchId)
        {
            if (!_searches.TryGetValue(searchId, out var search))
            {
                return new List<DownloadDecision>();
            }

            lock (search.Sync)
            {
                return DeDupe(search.Decisions);
            }
        }

        private static List<DownloadDecision> DeDupe(List<DownloadDecision> decisions)
        {
            return decisions
                .GroupBy(GetKey)
                .Select(group => group.First())
                .ToList();
        }

        private static string GetKey(DownloadDecision decision)
        {
            var guid = (decision.RemoteEpisode?.Release ?? decision.RemoteMovie?.Release)?.Guid;

            // Unidentifiable releases are kept rather than collapsed into a single result.
            return guid.IsNullOrWhiteSpace() ? Guid.NewGuid().ToString() : guid;
        }

        private void Prune()
        {
            var threshold = DateTime.UtcNow - Expiry;

            foreach (var pair in _searches)
            {
                if (pair.Value.LastTouchedUtc < threshold)
                {
                    Cancel(pair.Key);
                }
            }
        }

        private class TrackedSearch
        {
            public object Sync { get; } = new object();
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public bool IsComplete { get; set; }
            public DateTime LastTouchedUtc { get; set; } = DateTime.UtcNow;
            public List<DownloadDecision> Decisions { get; set; } = new List<DownloadDecision>();
        }
    }
}
