using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Queue
{
    public class Queue : ModelBase
    {
        public Series Series { get; set; }

        public Movie Movie { get; set; }

        public int? SeasonNumber { get; set; }

        [Obsolete]
        public Episode Episode { get; set; }

        public List<Episode> Episodes { get; set; }
        public List<Language> Languages { get; set; }
        public QualityModel Quality { get; set; }
        public decimal Size { get; set; }
        public string Title { get; set; }
        public decimal SizeLeft { get; set; }
        public TimeSpan? TimeLeft { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
        public DateTime? Added { get; set; }
        public QueueStatus Status { get; set; }
        public TrackedDownloadStatus? TrackedDownloadStatus { get; set; }
        public TrackedDownloadState? TrackedDownloadState { get; set; }
        public List<TrackedDownloadStatusMessage> StatusMessages { get; set; }
        public string DownloadId { get; set; }
        public RemoteEpisode RemoteEpisode { get; set; }
        public RemoteMovie RemoteMovie { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string DownloadClient { get; set; }
        public bool DownloadClientHasPostImportCategory { get; set; }
        public string Indexer { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }

        // Set when Theoriarr worked out where the download's files really are (the client's
        // reported path was wrong). The queue offers a "fix path" action for supported clients;
        // nothing is moved automatically.
        public string SuggestedOutputPath { get; set; }

        // Whether the download client can be told to move the content to SuggestedOutputPath.
        public bool PathCorrectionSupported { get; set; }

        // Whether the path Theoriarr would import from is missing on disk.
        public bool PathNotAccessible { get; set; }

        // The download is blocked because a media file could not be read/probed (corrupt). The
        // queue offers a "delete and redownload" action.
        public bool CorruptFileDetected { get; set; }
    }
}
