using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Queue;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Queue
{
    [V3ApiController("queue")]
    [AppSubsystem(AppSubsystem.Series)]
    public class QueueActionController : Controller
    {
        private const string RepositoryUrl = "https://github.com/MagicBOTAlex/Theoriarr";

        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IDownloadService _downloadService;
        private readonly IQueueService _queueService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public QueueActionController(IPendingReleaseService pendingReleaseService,
                                     IDownloadService downloadService,
                                     IQueueService queueService,
                                     ITrackedDownloadService trackedDownloadService,
                                     IProvideDownloadClient downloadClientProvider,
                                     ISubsystemAccessor subsystemAccessor)
        {
            _pendingReleaseService = pendingReleaseService;
            _downloadService = downloadService;
            _queueService = queueService;
            _trackedDownloadService = trackedDownloadService;
            _downloadClientProvider = downloadClientProvider;
            _subsystemAccessor = subsystemAccessor;
        }

        [HttpPost("grab/{id:int}")]
        public async Task<object> Grab([FromRoute] int id)
        {
            var pendingRelease = FindPendingQueueItem(id);

            if (pendingRelease == null)
            {
                throw new NotFoundException();
            }

            await GrabPendingRelease(pendingRelease);

            return new { };
        }

        [HttpPost("grab/bulk")]
        [Consumes("application/json")]
        public async Task<object> Grab([FromBody] QueueBulkResource resource)
        {
            foreach (var id in resource.Ids)
            {
                var pendingRelease = FindPendingQueueItem(id);

                if (pendingRelease == null)
                {
                    throw new NotFoundException();
                }

                await GrabPendingRelease(pendingRelease);
            }

            return new { };
        }

        private async Task GrabPendingRelease(NzbDrone.Core.Queue.Queue pendingRelease)
        {
            if (pendingRelease.RemoteMovie != null)
            {
                await _downloadService.DownloadReport(pendingRelease.RemoteMovie, null);
            }
            else
            {
                await _downloadService.DownloadReport(pendingRelease.RemoteEpisode, null);
            }
        }

        [HttpPost("fixpath/{id:int}")]
        public object FixPath([FromRoute] int id)
        {
            var queueItem = _queueService.Find(id);

            if (!IsVisible(queueItem))
            {
                throw new NotFoundException();
            }

            var trackedDownload = _trackedDownloadService.Find(queueItem.DownloadId);

            if (trackedDownload == null)
            {
                throw new NotFoundException();
            }

            var suggestedPath = trackedDownload.ImportItem?.SuggestedOutputPath;

            if (suggestedPath.IsNullOrWhiteSpace())
            {
                throw new BadRequestException("No corrected path is available for this download.");
            }

            var downloadClient = _downloadClientProvider.Get(trackedDownload.DownloadItem.DownloadClientInfo.Id);

            if (!downloadClient.SupportsPathCorrection)
            {
                throw new BadRequestException($"Automatic path fixing is not available for {downloadClient.Name}. Please open an issue at {RepositoryUrl}/issues/new.");
            }

            downloadClient.SetDownloadLocation(trackedDownload.DownloadItem, suggestedPath);

            return new { };
        }

        // Queue ids are global (one shared download queue), so an id may point at a
        // release owned by the other subsystem. Only operate on items that belong to the
        // requesting domain: the series key must never grab or mutate a movie item and
        // vice versa. Items with neither a remote movie nor a remote episode (unknown)
        // stay reachable, matching the queue list's includeUnknown behaviour.
        private NzbDrone.Core.Queue.Queue FindPendingQueueItem(int id)
        {
            var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

            return IsVisible(pendingRelease) ? pendingRelease : null;
        }

        private bool IsVisible(NzbDrone.Core.Queue.Queue queueItem)
        {
            if (queueItem == null)
            {
                return false;
            }

            return _subsystemAccessor.Subsystem == AppSubsystem.Movies
                ? queueItem.RemoteEpisode == null
                : queueItem.RemoteMovie == null;
        }
    }
}
