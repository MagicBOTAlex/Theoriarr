using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Blocklisting
{
    // D3: shared-table superset. A row belongs to either the SERIES or MOVIES domain.
    // The opposite domain's key columns sit at their default (0 / NULL) and MediaType
    // records which pipeline wrote the row.
    public class Blocklist : ModelBase
    {
        public int SeriesId { get; set; }
        public Series Series { get; set; }
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
        public MediaType MediaType { get; set; }
        public List<int> EpisodeIds { get; set; }
        public string SourceTitle { get; set; }
        public QualityModel Quality { get; set; }
        public DateTime Date { get; set; }
        public DateTime? PublishedDate { get; set; }
        public long? Size { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string Indexer { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string TorrentInfoHash { get; set; }
        public List<Language> Languages { get; set; }
    }
}
