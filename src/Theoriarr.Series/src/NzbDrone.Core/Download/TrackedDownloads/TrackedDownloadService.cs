using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.History;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Download.TrackedDownloads
{
    public interface ITrackedDownloadService
    {
        TrackedDownload Find(string downloadId);
        void StopTracking(string downloadId);
        void StopTracking(List<string> downloadIds);
        TrackedDownload TrackDownload(DownloadClientDefinition downloadClient, DownloadClientItem downloadItem);
        List<TrackedDownload> GetTrackedDownloads();
        void UpdateTrackable(List<TrackedDownload> trackedDownloads);
    }

    public class TrackedDownloadService : ITrackedDownloadService,
                                          IHandle<EpisodeGrabbedEvent>,
                                          IHandle<MovieGrabbedEvent>,
                                          IHandle<EpisodeInfoRefreshedEvent>,
                                          IHandle<SeriesAddedEvent>,
                                          IHandle<SeriesEditedEvent>,
                                          IHandle<SeriesBulkEditedEvent>,
                                          IHandle<SeriesDeletedEvent>,
                                          IHandle<EpisodeFileDeletedEvent>,
                                          IHandle<MovieAddedEvent>,
                                          IHandle<MovieEditedEvent>,
                                          IHandle<MoviesBulkEditedEvent>,
                                          IHandle<MoviesDeletedEvent>,
                                          IHandle<MovieFileDeletedEvent>
    {
        private readonly IParsingService _parsingService;
        private readonly IHistoryService _historyService;
        private readonly ISeriesService _seriesService;
        private readonly IDownloadHistoryService _downloadHistoryService;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly IRemoteMovieAggregationService _movieAggregationService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private readonly ICached<TrackedDownload> _cache;

        public TrackedDownloadService(IParsingService parsingService,
                                      IHistoryService historyService,
                                      ISeriesService seriesService,
                                      IDownloadHistoryService downloadHistoryService,
                                      IRemoteEpisodeAggregationService aggregationService,
                                      IRemoteMovieAggregationService movieAggregationService,
                                      ICustomFormatCalculationService formatCalculator,
                                      IEventAggregator eventAggregator,
                                      ICacheManager cacheManager,
                                      Logger logger)
        {
            _parsingService = parsingService;
            _historyService = historyService;
            _seriesService = seriesService;
            _downloadHistoryService = downloadHistoryService;
            _aggregationService = aggregationService;
            _movieAggregationService = movieAggregationService;
            _formatCalculator = formatCalculator;
            _eventAggregator = eventAggregator;
            _logger = logger;

            _cache = cacheManager.GetCache<TrackedDownload>(GetType());
        }

        public TrackedDownload Find(string downloadId)
        {
            return _cache.Find(downloadId);
        }

        public void StopTracking(string downloadId)
        {
            var trackedDownload = _cache.Find(downloadId);

            _cache.Remove(downloadId);
            _eventAggregator.PublishEvent(new TrackedDownloadsRemovedEvent(new List<TrackedDownload> { trackedDownload }));
        }

        public void StopTracking(List<string> downloadIds)
        {
            var trackedDownloads = new List<TrackedDownload>();

            foreach (var downloadId in downloadIds)
            {
                var trackedDownload = _cache.Find(downloadId);
                _cache.Remove(downloadId);
                trackedDownloads.Add(trackedDownload);
            }

            _eventAggregator.PublishEvent(new TrackedDownloadsRemovedEvent(trackedDownloads));
        }

        public TrackedDownload TrackDownload(DownloadClientDefinition downloadClient, DownloadClientItem downloadItem)
        {
            var existingItem = Find(downloadItem.DownloadId);

            if (existingItem != null && existingItem.State != TrackedDownloadState.Downloading)
            {
                LogItemChange(existingItem, existingItem.DownloadItem, downloadItem);

                // A changed output path means the item was moved (e.g. the user fixed the
                // download location). Clear the retry cap so the import can run against the new
                // path instead of staying stuck on the manual-interaction state.
                if (existingItem.DownloadItem?.OutputPath != downloadItem.OutputPath)
                {
                    existingItem.ImportAttempts = 0;
                    existingItem.HasNotifiedManualInteractionRequired = false;
                }

                existingItem.DownloadItem = downloadItem;
                existingItem.IsTrackable = true;

                return existingItem;
            }

            var trackedDownload = new TrackedDownload
            {
                DownloadClient = downloadClient.Id,
                DownloadItem = downloadItem,
                Protocol = downloadClient.Protocol,
                IsTrackable = true,
                HasNotifiedManualInteractionRequired = existingItem?.HasNotifiedManualInteractionRequired ?? false
            };

            try
            {
                var downloadHistory = _downloadHistoryService.GetLatestDownloadHistoryItem(downloadItem.DownloadId);

                if (downloadHistory != null)
                {
                    var state = GetStateFromHistory(downloadHistory.EventType);
                    trackedDownload.State = state;
                }

                var movieHistoryItems = (_historyService.FindMovieByDownloadId(downloadItem.DownloadId) ?? new List<MovieHistory>())
                    .OrderByDescending(h => h.Date)
                    .ToList();
                var episodeHistoryItems = _historyService.FindByDownloadId(downloadItem.DownloadId) ?? new List<EpisodeHistory>();

                var mediaType = ResolveMediaType(downloadClient, downloadItem, downloadHistory, episodeHistoryItems, movieHistoryItems);

                if (mediaType == MediaType.Movie)
                {
                    TrackMovieDownload(trackedDownload, downloadHistory, movieHistoryItems);
                }
                else if (mediaType == MediaType.Series)
                {
                    TrackEpisodeDownload(trackedDownload, downloadHistory);
                }
                else
                {
                    // No authoritative signal (no grab history and a shared/ambiguous category):
                    // try the series parse first, and only fall back to the movie parse when no
                    // episode subject was found and there is no series grab history. The subject is
                    // set exclusively, so the download can never become both.
                    TrackEpisodeDownload(trackedDownload, downloadHistory);

                    if (trackedDownload.RemoteEpisode?.Series == null && episodeHistoryItems.Empty())
                    {
                        TrackMovieDownload(trackedDownload, downloadHistory, movieHistoryItems);
                    }
                }

                trackedDownload.DownloadItem.MediaType = trackedDownload.RemoteMovie != null
                    ? MediaType.Movie
                    : trackedDownload.RemoteEpisode != null
                        ? MediaType.Series
                        : null;
            }
            catch (MultipleMoviesFoundException e)
            {
                _logger.Debug(e, "Found multiple movies for " + downloadItem.Title);

                trackedDownload.Warn("Unable to import automatically, found multiple movies: {0}", string.Join(", ", e.Movies));
            }
            catch (MultipleSeriesFoundException e)
            {
                _logger.Debug(e, "Found multiple series for " + downloadItem.Title);

                trackedDownload.Warn("Unable to import automatically, found multiple series: {0}", string.Join(", ", e.Series));
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Failed to find episode for " + downloadItem.Title);

                trackedDownload.Warn("Unable to parse episodes from title");
            }

            LogItemChange(trackedDownload, existingItem?.DownloadItem, trackedDownload.DownloadItem);

            _cache.Set(trackedDownload.DownloadItem.DownloadId, trackedDownload);
            return trackedDownload;
        }

        // Authoritative order: the grab history Theoriarr itself recorded, then a genuinely
        // distinct category (series vs movie labelled differently), otherwise unknown. The
        // unified default category ("theoriarr") is shared, so it is deliberately not treated as
        // a signal and the caller falls back to parsing.
        private MediaType? ResolveMediaType(DownloadClientDefinition downloadClient,
                                            DownloadClientItem downloadItem,
                                            DownloadHistory downloadHistory,
                                            List<EpisodeHistory> episodeHistoryItems,
                                            List<MovieHistory> movieHistoryItems)
        {
            if (movieHistoryItems.Any() || downloadHistory is { MovieId: > 0 })
            {
                return MediaType.Movie;
            }

            if (episodeHistoryItems.Any() || downloadHistory is { SeriesId: > 0 })
            {
                return MediaType.Series;
            }

            if (downloadClient.Settings is IDownloadClientCategorySettings categorySettings)
            {
                if (DownloadClientCategoryHelper.IsMovieCategory(downloadItem.Category, categorySettings))
                {
                    return MediaType.Movie;
                }

                if (DownloadClientCategoryHelper.IsSeriesCategory(downloadItem.Category, categorySettings))
                {
                    return MediaType.Series;
                }
            }

            return null;
        }

        private void TrackEpisodeDownload(TrackedDownload trackedDownload, DownloadHistory downloadHistory)
        {
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(trackedDownload.DownloadItem.Title);

            if (parsedEpisodeInfo != null)
            {
                trackedDownload.SetEpisode(downloadHistory is { EventType: DownloadHistoryEventType.DownloadImported }
                    ? _parsingService.Map(parsedEpisodeInfo, _seriesService.GetSeries(downloadHistory.SeriesId))
                    : _parsingService.Map(parsedEpisodeInfo, 0, 0, null));
            }

            var historyItems = _historyService.FindByDownloadId(trackedDownload.DownloadItem.DownloadId)
                .OrderByDescending(h => h.Date)
                .ToList();

            if (historyItems.Any())
            {
                var firstHistoryItem = historyItems.First();
                var grabbedEvent = historyItems.FirstOrDefault(v => v.EventType == EpisodeHistoryEventType.Grabbed);

                trackedDownload.Indexer = grabbedEvent?.Data?.GetValueOrDefault("indexer");
                trackedDownload.Added = grabbedEvent?.Date;

                if (parsedEpisodeInfo == null ||
                    trackedDownload.RemoteEpisode?.Series == null ||
                    trackedDownload.RemoteEpisode.Episodes.Empty())
                {
                    // Try parsing the original source title and if that fails, try parsing it as a special
                    // TODO: Pass the TVDB ID and TVRage IDs in as well so we have a better chance for finding the item
                    parsedEpisodeInfo = Parser.Parser.ParseTitle(firstHistoryItem.SourceTitle) ??
                                        _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, firstHistoryItem.SourceTitle, 0, 0, null);

                    if (parsedEpisodeInfo != null)
                    {
                        trackedDownload.SetEpisode(_parsingService.Map(parsedEpisodeInfo,
                            firstHistoryItem.SeriesId,
                            historyItems.Where(v => v.EventType == EpisodeHistoryEventType.Grabbed)
                                .Select(h => h.EpisodeId).Distinct()));
                    }
                }

                if (trackedDownload.RemoteEpisode != null)
                {
                    trackedDownload.RemoteEpisode.Release ??= new ReleaseInfo();
                    trackedDownload.RemoteEpisode.Release.Indexer = trackedDownload.Indexer;
                    trackedDownload.RemoteEpisode.Release.Title = trackedDownload.RemoteEpisode.ParsedEpisodeInfo?.ReleaseTitle;

                    if (Enum.TryParse(grabbedEvent?.Data?.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                    {
                        trackedDownload.RemoteEpisode.Release.IndexerFlags = flags;
                    }

                    if (downloadHistory != null)
                    {
                        trackedDownload.RemoteEpisode.Release.IndexerId = downloadHistory.IndexerId;
                    }
                }
            }

            if (trackedDownload.RemoteEpisode != null)
            {
                _aggregationService.Augment(trackedDownload.RemoteEpisode);

                // Calculate custom formats
                trackedDownload.RemoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(trackedDownload.RemoteEpisode, trackedDownload.DownloadItem.TotalSize);
            }

            // Track it so it can be displayed in the queue even though we can't determine which series it is for
            if (trackedDownload.RemoteEpisode == null)
            {
                _logger.Trace("No Episode found for download '{0}'", trackedDownload.DownloadItem.Title);
            }
        }

        private void TrackMovieDownload(TrackedDownload trackedDownload, DownloadHistory downloadHistory, List<MovieHistory> historyItems)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(trackedDownload.DownloadItem.Title);

            if (parsedMovieInfo != null)
            {
                trackedDownload.SetMovie(downloadHistory is { EventType: DownloadHistoryEventType.DownloadImported, MovieId: > 0 }
                    ? _parsingService.Map(parsedMovieInfo, downloadHistory.MovieId)
                    : _parsingService.Map(parsedMovieInfo, "", 0, null));
            }

            if (historyItems.Any())
            {
                var firstHistoryItem = historyItems.First();
                var grabbedEvent = historyItems.FirstOrDefault(v => v.EventType == MovieHistoryEventType.Grabbed);

                trackedDownload.Indexer = grabbedEvent?.Data?.GetValueOrDefault("indexer");
                trackedDownload.Added = grabbedEvent?.Date;

                if (parsedMovieInfo == null ||
                    trackedDownload.RemoteMovie?.Movie == null)
                {
                    parsedMovieInfo = Parser.Parser.ParseMovieTitle(firstHistoryItem.SourceTitle);

                    if (parsedMovieInfo != null)
                    {
                        trackedDownload.SetMovie(_parsingService.Map(parsedMovieInfo, firstHistoryItem.MovieId));
                    }
                }

                if (trackedDownload.RemoteMovie != null)
                {
                    trackedDownload.RemoteMovie.Release ??= new ReleaseInfo();
                    trackedDownload.RemoteMovie.Release.Indexer = trackedDownload.Indexer;
                    trackedDownload.RemoteMovie.Release.Title = trackedDownload.RemoteMovie.ParsedMovieInfo?.ReleaseTitle;

                    if (Enum.TryParse(grabbedEvent?.Data?.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                    {
                        trackedDownload.RemoteMovie.Release.IndexerFlags = flags;
                    }

                    if (downloadHistory != null)
                    {
                        trackedDownload.RemoteMovie.Release.IndexerId = downloadHistory.IndexerId;
                    }
                }
            }

            if (trackedDownload.RemoteMovie != null)
            {
                _movieAggregationService.Augment(trackedDownload.RemoteMovie);

                // Calculate custom formats
                trackedDownload.RemoteMovie.CustomFormats = _formatCalculator.ParseCustomFormat(trackedDownload.RemoteMovie, trackedDownload.DownloadItem.TotalSize);
            }

            // Track it so it can be displayed in the queue even though we can't determine which movie it is for
            if (trackedDownload.RemoteMovie == null)
            {
                _logger.Trace("No Movie found for download '{0}'", trackedDownload.DownloadItem.Title);
            }
        }

        public List<TrackedDownload> GetTrackedDownloads()
        {
            return _cache.Values.ToList();
        }

        public void UpdateTrackable(List<TrackedDownload> trackedDownloads)
        {
            var untrackable = GetTrackedDownloads().ExceptBy(t => t.DownloadItem.DownloadId, trackedDownloads, t => t.DownloadItem.DownloadId, StringComparer.CurrentCulture).ToList();

            foreach (var trackedDownload in untrackable)
            {
                trackedDownload.IsTrackable = false;
            }
        }

        private void LogItemChange(TrackedDownload trackedDownload, DownloadClientItem existingItem, DownloadClientItem downloadItem)
        {
            if (existingItem == null ||
                existingItem.Status != downloadItem.Status ||
                existingItem.CanBeRemoved != downloadItem.CanBeRemoved ||
                existingItem.CanMoveFiles != downloadItem.CanMoveFiles)
            {
                _logger.Debug("Tracking '{0}:{1}': ClientState={2}{3} TheoriarrStage={4} Episode='{5}' OutputPath={6}.",
                    downloadItem.DownloadClientInfo.Name,
                    downloadItem.Title,
                    downloadItem.Status,
                    downloadItem.CanBeRemoved ? "" : downloadItem.CanMoveFiles ? " (busy)" : " (readonly)",
                    trackedDownload.State,
                    trackedDownload.RemoteEpisode?.ParsedEpisodeInfo,
                    downloadItem.OutputPath);
            }
        }

        private void UpdateCachedItem(TrackedDownload trackedDownload)
        {
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(trackedDownload.DownloadItem.Title);

            trackedDownload.SetEpisode(parsedEpisodeInfo == null ? null : _parsingService.Map(parsedEpisodeInfo, 0, 0, null));

            if (trackedDownload.RemoteEpisode != null)
            {
                _aggregationService.Augment(trackedDownload.RemoteEpisode);
            }
        }

        private void UpdateCachedMovie(TrackedDownload trackedDownload)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(trackedDownload.DownloadItem.Title);

            trackedDownload.SetMovie(parsedMovieInfo == null ? null : _parsingService.Map(parsedMovieInfo, "", 0, null));

            if (trackedDownload.RemoteMovie != null)
            {
                _movieAggregationService.Augment(trackedDownload.RemoteMovie);
            }
        }

        private static TrackedDownloadState GetStateFromHistory(DownloadHistoryEventType eventType)
        {
            switch (eventType)
            {
                case DownloadHistoryEventType.DownloadImported:
                    return TrackedDownloadState.Imported;
                case DownloadHistoryEventType.DownloadFailed:
                    return TrackedDownloadState.Failed;
                case DownloadHistoryEventType.DownloadIgnored:
                    return TrackedDownloadState.Ignored;
                default:
                    return TrackedDownloadState.Downloading;
            }
        }

        public void Handle(EpisodeGrabbedEvent message)
        {
            if (message.DownloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var trackedDownload = _cache.Find(message.DownloadId);

            if (trackedDownload is { State: TrackedDownloadState.Imported or
                                            TrackedDownloadState.Failed or
                                            TrackedDownloadState.Ignored })
            {
                _cache.Remove(message.DownloadId);
            }
        }

        public void Handle(EpisodeInfoRefreshedEvent message)
        {
            var needsToUpdate = false;

            foreach (var episode in message.Removed)
            {
                var cachedItems = _cache.Values.Where(t =>
                                            t.RemoteEpisode?.Episodes != null &&
                                            t.RemoteEpisode.Episodes.Any(e => e.Id == episode.Id))
                                        .ToList();

                if (cachedItems.Any())
                {
                    needsToUpdate = true;
                }

                cachedItems.ForEach(UpdateCachedItem);
            }

            if (needsToUpdate)
            {
                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(SeriesAddedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteMovie == null &&
                    (t.RemoteEpisode?.Series == null ||
                     message.Series?.TvdbId == t.RemoteEpisode.Series.TvdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedItem);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(SeriesEditedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteEpisode?.Series != null &&
                    (t.RemoteEpisode.Series.Id == message.Series?.Id || t.RemoteEpisode.Series.TvdbId == message.Series?.TvdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedItem);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(SeriesBulkEditedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteEpisode?.Series != null &&
                    message.Series.Any(s => s.Id == t.RemoteEpisode.Series.Id || s.TvdbId == t.RemoteEpisode.Series.TvdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedItem);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(SeriesDeletedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteEpisode?.Series != null &&
                    message.Series.Any(s => s.Id == t.RemoteEpisode.Series.Id || s.TvdbId == t.RemoteEpisode.Series.TvdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedItem);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(MovieGrabbedEvent message)
        {
            if (message.DownloadId.IsNullOrWhiteSpace())
            {
                return;
            }

            var trackedDownload = _cache.Find(message.DownloadId);

            if (trackedDownload is { State: TrackedDownloadState.Imported or
                                            TrackedDownloadState.Failed or
                                            TrackedDownloadState.Ignored })
            {
                _cache.Remove(message.DownloadId);
            }
        }

        public void Handle(MovieAddedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteEpisode == null &&
                    (t.RemoteMovie?.Movie == null ||
                     message.Movie?.TmdbId == t.RemoteMovie.Movie.TmdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedMovie);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(MovieEditedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteMovie?.Movie != null &&
                    (t.RemoteMovie.Movie.Id == message.Movie?.Id || t.RemoteMovie.Movie.TmdbId == message.Movie?.TmdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedMovie);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(MoviesBulkEditedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteMovie?.Movie != null &&
                    message.Movies.Any(m => m.Id == t.RemoteMovie.Movie.Id || m.TmdbId == t.RemoteMovie.Movie.TmdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedMovie);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(MoviesDeletedEvent message)
        {
            var cachedItems = _cache.Values
                .Where(t =>
                    t.RemoteMovie?.Movie != null &&
                    message.Movies.Any(m => m.Id == t.RemoteMovie.Movie.Id || m.TmdbId == t.RemoteMovie.Movie.TmdbId))
                .ToList();

            if (cachedItems.Any())
            {
                cachedItems.ForEach(UpdateCachedMovie);

                _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
            }
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            ResetImportAttemptsForBlockedDownloads(t => t.RemoteEpisode?.Series?.Id == message.EpisodeFile.SeriesId);
        }

        public void Handle(MovieFileDeletedEvent message)
        {
            ResetImportAttemptsForBlockedDownloads(t => t.RemoteMovie?.Movie?.Id == message.MovieFile.MovieId);
        }

        // A destination conflict is fixed by changing/removing the existing file. Clear the retry
        // cap for the blocked downloads that targeted it so they are allowed to import again.
        private void ResetImportAttemptsForBlockedDownloads(Func<TrackedDownload, bool> predicate)
        {
            var blockedDownloads = _cache.Values
                .Where(t => t.State == TrackedDownloadState.ImportBlocked && predicate(t))
                .ToList();

            if (!blockedDownloads.Any())
            {
                return;
            }

            foreach (var trackedDownload in blockedDownloads)
            {
                trackedDownload.ImportAttempts = 0;
                trackedDownload.HasNotifiedManualInteractionRequired = false;
            }

            _eventAggregator.PublishEvent(new TrackedDownloadRefreshedEvent(GetTrackedDownloads()));
        }
    }
}
