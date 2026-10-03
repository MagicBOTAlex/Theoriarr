using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Download
{
    public interface IDownloadService
    {
        Task DownloadReport(RemoteEpisode remoteEpisode, int? downloadClientId);

        // D3/D7: movie grabs share the download service; a successful grab publishes
        // MovieGrabbedEvent which the unified HistoryService records as a MovieHistory row.
        Task DownloadReport(RemoteMovie remoteMovie, int? downloadClientId);
    }

    public class DownloadService : IDownloadService
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IDownloadClientStatusService _downloadClientStatusService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISeedConfigProvider _seedConfigProvider;
        private readonly Logger _logger;

        public DownloadService(IProvideDownloadClient downloadClientProvider,
                               IDownloadClientStatusService downloadClientStatusService,
                               IIndexerFactory indexerFactory,
                               IIndexerStatusService indexerStatusService,
                               IRateLimitService rateLimitService,
                               IEventAggregator eventAggregator,
                               ISeedConfigProvider seedConfigProvider,
                               Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _downloadClientStatusService = downloadClientStatusService;
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _rateLimitService = rateLimitService;
            _eventAggregator = eventAggregator;
            _seedConfigProvider = seedConfigProvider;
            _logger = logger;
        }

        public async Task DownloadReport(RemoteEpisode remoteEpisode, int? downloadClientId)
        {
            var filterBlockedClients = remoteEpisode.Release.PendingReleaseReason == PendingReleaseReason.DownloadClientUnavailable;

            var tags = remoteEpisode.Series?.Tags;

            if (downloadClientId.HasValue)
            {
                var specificClient = _downloadClientProvider.Get(downloadClientId.Value);
                await DownloadReport(remoteEpisode, specificClient);

                return;
            }

            var mediaType = remoteEpisode.Series?.SeriesType == SeriesTypes.Anime ? MediaType.Anime : MediaType.Series;

            var availableClients = _downloadClientProvider.GetDownloadClients(
                remoteEpisode.Release.DownloadProtocol,
                remoteEpisode.Release.IndexerId,
                filterBlockedClients,
                tags,
                mediaType).ToList();

            if (!availableClients.Any())
            {
                throw new DownloadClientUnavailableException($"No {remoteEpisode.Release.DownloadProtocol} download client available");
            }

            var triedClients = new HashSet<int>();

            foreach (var downloadClient in availableClients)
            {
                if (triedClients.Contains(downloadClient.Definition.Id))
                {
                    continue;
                }

                try
                {
                    _logger.Debug("Attempting download with client: {0}", downloadClient.Definition.Name);
                    await DownloadReport(remoteEpisode, downloadClient);

                    _downloadClientProvider.ReportSuccessfulDownloadClient(
                        remoteEpisode.Release.DownloadProtocol,
                        downloadClient.Definition.Id);

                    return;
                }
                catch (DownloadClientException ex)
                {
                    _logger.Trace(ex, "Unable to add report to download client: {0}", downloadClient.Definition.Name);
                    triedClients.Add(downloadClient.Definition.Id);
                }
                catch (Exception ex)
                {
                    // Rethrow specific exceptions that should not trigger a fallback
                    if (ex is ReleaseDownloadException)
                    {
                        throw;
                    }

                    _logger.Trace(ex, "Unable to add report to download client: {0}", downloadClient.Definition.Name);
                    triedClients.Add(downloadClient.Definition.Id);
                }
            }

            throw new DownloadClientUnavailableException("All '{0}' download clients failed", remoteEpisode.Release.DownloadProtocol);
        }

        private async Task DownloadReport(RemoteEpisode remoteEpisode, IDownloadClient downloadClient)
        {
            Ensure.That(remoteEpisode.Series, () => remoteEpisode.Series).IsNotNull();
            Ensure.That(remoteEpisode.Episodes, () => remoteEpisode.Episodes).HasItems();

            var downloadTitle = remoteEpisode.Release.Title;

            if (downloadClient == null)
            {
                throw new DownloadClientUnavailableException($"{remoteEpisode.Release.DownloadProtocol} Download client isn't configured yet");
            }

            // Get the seed configuration for this release.
            remoteEpisode.SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(remoteEpisode);

            // Limit grabs to 2 per second.
            if (remoteEpisode.Release.DownloadUrl.IsNotNullOrWhiteSpace() && !remoteEpisode.Release.DownloadUrl.StartsWith("magnet:"))
            {
                var url = new HttpUri(remoteEpisode.Release.DownloadUrl);
                await _rateLimitService.WaitAndPulseAsync(url.Host, TimeSpan.FromSeconds(2));
            }

            IIndexer indexer = null;

            if (remoteEpisode.Release.IndexerId > 0)
            {
                indexer = _indexerFactory.GetInstance(_indexerFactory.Get(remoteEpisode.Release.IndexerId));
            }

            string downloadClientId;
            try
            {
                downloadClientId = await downloadClient.Download(remoteEpisode, indexer);
                _downloadClientStatusService.RecordSuccess(downloadClient.Definition.Id);
                _indexerStatusService.RecordSuccess(remoteEpisode.Release.IndexerId);
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Trace("Release {0} no longer available on indexer.", remoteEpisode);
                throw;
            }
            catch (ReleaseBlockedException)
            {
                _logger.Trace("Release {0} previously added to blocklist, not sending to download client again.", remoteEpisode);
                throw;
            }
            catch (DownloadClientRejectedReleaseException)
            {
                _logger.Trace("Release {0} rejected by download client, possible duplicate.", remoteEpisode);
                throw;
            }
            catch (ReleaseDownloadException ex)
            {
                if (ex.InnerException is TooManyRequestsException http429)
                {
                    _indexerStatusService.RecordFailure(remoteEpisode.Release.IndexerId, http429.RetryAfter);
                }
                else
                {
                    _indexerStatusService.RecordFailure(remoteEpisode.Release.IndexerId);
                }

                throw;
            }

            var episodeGrabbedEvent = new EpisodeGrabbedEvent(remoteEpisode);
            episodeGrabbedEvent.DownloadClient = downloadClient.Name;
            episodeGrabbedEvent.DownloadClientId = downloadClient.Definition.Id;
            episodeGrabbedEvent.DownloadClientName = downloadClient.Definition.Name;

            if (downloadClientId.IsNotNullOrWhiteSpace())
            {
                episodeGrabbedEvent.DownloadId = downloadClientId;
            }

            _logger.ProgressInfo("Report sent to {0}. Indexer {1}. {2}", downloadClient.Definition.Name, remoteEpisode.Release.Indexer, downloadTitle);
            _eventAggregator.PublishEvent(episodeGrabbedEvent);
        }

        public async Task DownloadReport(RemoteMovie remoteMovie, int? downloadClientId)
        {
            var filterBlockedClients = remoteMovie.Release.PendingReleaseReason == PendingReleaseReason.DownloadClientUnavailable;

            var tags = remoteMovie.Movie?.Tags;

            if (downloadClientId.HasValue)
            {
                var specificClient = _downloadClientProvider.Get(downloadClientId.Value);
                await DownloadReport(remoteMovie, specificClient);

                return;
            }

            var availableClients = _downloadClientProvider.GetDownloadClients(
                remoteMovie.Release.DownloadProtocol,
                remoteMovie.Release.IndexerId,
                filterBlockedClients,
                tags,
                MediaType.Movie).ToList();

            if (!availableClients.Any())
            {
                throw new DownloadClientUnavailableException($"No {remoteMovie.Release.DownloadProtocol} download client available");
            }

            var triedClients = new HashSet<int>();

            foreach (var downloadClient in availableClients)
            {
                if (triedClients.Contains(downloadClient.Definition.Id))
                {
                    continue;
                }

                try
                {
                    _logger.Debug("Attempting download with client: {0}", downloadClient.Definition.Name);
                    await DownloadReport(remoteMovie, downloadClient);

                    _downloadClientProvider.ReportSuccessfulDownloadClient(
                        remoteMovie.Release.DownloadProtocol,
                        downloadClient.Definition.Id);

                    return;
                }
                catch (DownloadClientException ex)
                {
                    _logger.Trace(ex, "Unable to add report to download client: {0}", downloadClient.Definition.Name);
                    triedClients.Add(downloadClient.Definition.Id);
                }
                catch (Exception ex)
                {
                    if (ex is ReleaseDownloadException)
                    {
                        throw;
                    }

                    _logger.Trace(ex, "Unable to add report to download client: {0}", downloadClient.Definition.Name);
                    triedClients.Add(downloadClient.Definition.Id);
                }
            }

            throw new DownloadClientUnavailableException("All '{0}' download clients failed", remoteMovie.Release.DownloadProtocol);
        }

        private async Task DownloadReport(RemoteMovie remoteMovie, IDownloadClient downloadClient)
        {
            Ensure.That(remoteMovie.Movie, () => remoteMovie.Movie).IsNotNull();

            var downloadTitle = remoteMovie.Release.Title;

            if (downloadClient == null)
            {
                throw new DownloadClientUnavailableException($"{remoteMovie.Release.DownloadProtocol} Download client isn't configured yet");
            }

            // Get the seed configuration for this release.
            remoteMovie.SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(remoteMovie);

            // Limit grabs to 2 per second.
            if (remoteMovie.Release.DownloadUrl.IsNotNullOrWhiteSpace() && !remoteMovie.Release.DownloadUrl.StartsWith("magnet:"))
            {
                var url = new HttpUri(remoteMovie.Release.DownloadUrl);
                await _rateLimitService.WaitAndPulseAsync(url.Host, TimeSpan.FromSeconds(2));
            }

            IIndexer indexer = null;

            if (remoteMovie.Release.IndexerId > 0)
            {
                indexer = _indexerFactory.GetInstance(_indexerFactory.Get(remoteMovie.Release.IndexerId));
            }

            string downloadClientId;
            try
            {
                downloadClientId = await downloadClient.Download(remoteMovie, indexer);
                _downloadClientStatusService.RecordSuccess(downloadClient.Definition.Id);
                _indexerStatusService.RecordSuccess(remoteMovie.Release.IndexerId);
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Trace("Release {0} no longer available on indexer.", remoteMovie);
                throw;
            }
            catch (ReleaseBlockedException)
            {
                _logger.Trace("Release {0} previously added to blocklist, not sending to download client again.", remoteMovie);
                throw;
            }
            catch (DownloadClientRejectedReleaseException)
            {
                _logger.Trace("Release {0} rejected by download client, possible duplicate.", remoteMovie);
                throw;
            }
            catch (ReleaseDownloadException ex)
            {
                if (ex.InnerException is TooManyRequestsException http429)
                {
                    _indexerStatusService.RecordFailure(remoteMovie.Release.IndexerId, http429.RetryAfter);
                }
                else
                {
                    _indexerStatusService.RecordFailure(remoteMovie.Release.IndexerId);
                }

                throw;
            }

            var movieGrabbedEvent = new MovieGrabbedEvent(remoteMovie);
            movieGrabbedEvent.DownloadClient = downloadClient.Name;
            movieGrabbedEvent.DownloadClientId = downloadClient.Definition.Id;
            movieGrabbedEvent.DownloadClientName = downloadClient.Definition.Name;

            if (downloadClientId.IsNotNullOrWhiteSpace())
            {
                movieGrabbedEvent.DownloadId = downloadClientId;
            }

            _logger.ProgressInfo("Report for {0} ({1}) sent to {2} from indexer {3}. {4}", remoteMovie.Movie.Title, remoteMovie.Movie.Year, downloadClient.Definition.Name, remoteMovie.Release.Indexer, downloadTitle);
            _eventAggregator.PublishEvent(movieGrabbedEvent);
        }
    }
}
