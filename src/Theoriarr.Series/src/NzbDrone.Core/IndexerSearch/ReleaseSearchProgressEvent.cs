using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.IndexerSearch
{
    public enum ReleaseSearchDomain
    {
        Series,
        Movies
    }

    public enum ReleaseSearchProgressStatus
    {
        Started,
        IndexerCompleted,
        Completed
    }

    public class ReleaseSearchIndexerProgress
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // Published while an interactive search fans out to indexers so the UI can show
    // per-indexer progress. SearchId correlates the events with the originating request.
    public class ReleaseSearchProgressEvent : IEvent
    {
        public string SearchId { get; set; }
        public ReleaseSearchDomain Domain { get; set; }
        public ReleaseSearchProgressStatus Status { get; set; }
        public List<ReleaseSearchIndexerProgress> Indexers { get; set; }
        public int? IndexerId { get; set; }
        public string IndexerName { get; set; }
        public int ReleaseCount { get; set; }
        public bool Failed { get; set; }
    }
}
