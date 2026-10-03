using System;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.TrackedDownloads
{
    public class TrackedDownload
    {
        public int DownloadClient { get; set; }
        public DownloadClientItem DownloadItem { get; set; }
        public DownloadClientItem ImportItem { get; set; }
        public TrackedDownloadState State { get; set; }
        public TrackedDownloadStatus Status { get; private set; }
        public RemoteEpisode RemoteEpisode { get; set; }
        public RemoteMovie RemoteMovie { get; set; }
        public TrackedDownloadStatusMessage[] StatusMessages { get; private set; }
        public DownloadProtocol Protocol { get; set; }
        public string Indexer { get; set; }
        public DateTime? Added { get; set; }
        public bool IsTrackable { get; set; }
        public bool HasNotifiedManualInteractionRequired { get; set; }

        // In-memory count of consecutive failed import attempts, used to stop the automatic
        // import retry loop for downloads that cannot be imported (e.g. a destination file that
        // already exists). Reset on a successful import and when the app restarts.
        public int ImportAttempts { get; set; }

        // True when the import failed because a media file could not be read/probed (i.e. it is
        // corrupt). The queue uses it to offer a "delete and redownload" action.
        public bool CorruptFileDetected { get; set; }

        public TrackedDownload()
        {
            StatusMessages = Array.Empty<TrackedDownloadStatusMessage>();
        }

        // The media subject is single-valued: a download is either a series download or a movie
        // download, never both. Assigning one clears the other so the two parallel queue services
        // can never emit a row for the same download twice.
        public void SetEpisode(RemoteEpisode remoteEpisode)
        {
            RemoteEpisode = remoteEpisode;

            if (remoteEpisode != null)
            {
                RemoteMovie = null;
            }
        }

        public void SetMovie(RemoteMovie remoteMovie)
        {
            RemoteMovie = remoteMovie;

            if (remoteMovie != null)
            {
                RemoteEpisode = null;
            }
        }

        public void Warn(string message, params object[] args)
        {
            var statusMessage = string.Format(message, args);
            Warn(new TrackedDownloadStatusMessage(DownloadItem.Title, statusMessage));
        }

        public void Warn(params TrackedDownloadStatusMessage[] statusMessages)
        {
            Status = TrackedDownloadStatus.Warning;
            StatusMessages = statusMessages;
        }

        public void Fail()
        {
            Status = TrackedDownloadStatus.Error;
            State = TrackedDownloadState.FailedPending;

            // Set CanBeRemoved to allow the failed item to be removed from the client
            DownloadItem.CanBeRemoved = true;
        }
    }

    public enum TrackedDownloadState
    {
        Downloading,
        ImportBlocked,
        ImportPending,
        Importing,
        Imported,
        FailedPending,
        Failed,
        Ignored
    }

    public enum TrackedDownloadStatus
    {
        Ok,
        Warning,
        Error
    }
}
