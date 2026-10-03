using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class ManualInteractionRequiredMessage
    {
        public string Message { get; set; }
        public Series Series { get; set; }
        public RemoteEpisode Episode { get; set; }
        public Movie Movie { get; set; }
        public RemoteMovie RemoteMovie { get; set; }
        public TrackedDownload TrackedDownload { get; set; }
        public QualityModel Quality { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public GrabbedReleaseInfo Release { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
