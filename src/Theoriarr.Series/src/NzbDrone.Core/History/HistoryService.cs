using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.History
{
    public interface IHistoryService
    {
        PagingSpec<EpisodeHistory> Paged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities);
        EpisodeHistory MostRecentForEpisode(int episodeId);
        List<EpisodeHistory> FindByEpisodeId(int episodeId);
        EpisodeHistory MostRecentForDownloadId(string downloadId);
        EpisodeHistory Get(int historyId);
        List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> Find(string downloadId, EpisodeHistoryEventType eventType);
        List<EpisodeHistory> FindByDownloadId(string downloadId);
        string FindDownloadId(EpisodeImportedEvent trackedDownload);
        List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType);

        // D3: movie history lives in the same "History" table as episode history.
        // Movie methods are named distinctly from the episode ones so their return
        // types don't collide (C# cannot overload on return type alone).
        QualityModel GetBestQualityInHistory(QualityProfile profile, int movieId);
        PagingSpec<MovieHistory> Paged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities);
        MovieHistory MostRecentForMovie(int movieId);
        MovieHistory GetMovie(int historyId);
        List<MovieHistory> FindByMovieId(int movieId);
        List<MovieHistory> FindMovieByDownloadId(string downloadId);
        List<MovieHistory> FindMovie(string downloadId, MovieHistoryEventType eventType);
        List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType);
        void UpdateMany(List<MovieHistory> toUpdate);
        string FindDownloadId(MovieFileImportedEvent trackedDownload);
        List<MovieHistory> SinceMovie(DateTime date, MovieHistoryEventType? eventType);
    }

    public class HistoryService : IHistoryService,
                                  IHandle<EpisodeGrabbedEvent>,
                                  IHandle<EpisodeImportedEvent>,
                                  IHandle<DownloadFailedEvent>,
                                  IHandle<EpisodeFileDeletedEvent>,
                                  IHandle<EpisodeFileRenamedEvent>,
                                  IHandle<SeriesDeletedEvent>,
                                  IHandle<DownloadIgnoredEvent>,
                                  IHandle<MovieGrabbedEvent>,
                                  IHandle<MovieFileImportedEvent>,
                                  IHandle<MovieFileDeletedEvent>,
                                  IHandle<MovieFileRenamedEvent>,
                                  IHandle<MoviesDeletedEvent>
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly Logger _logger;

        public HistoryService(IHistoryRepository historyRepository, Logger logger)
        {
            _historyRepository = historyRepository;
            _logger = logger;
        }

        public PagingSpec<EpisodeHistory> Paged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities)
        {
            return _historyRepository.GetPaged(pagingSpec, languages, qualities);
        }

        public PagingSpec<MovieHistory> Paged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities)
        {
            return _historyRepository.GetPaged(pagingSpec, languages, qualities);
        }

        public EpisodeHistory MostRecentForEpisode(int episodeId)
        {
            return _historyRepository.MostRecentForEpisode(episodeId);
        }

        public List<EpisodeHistory> FindByEpisodeId(int episodeId)
        {
            return _historyRepository.FindByEpisodeId(episodeId);
        }

        public EpisodeHistory MostRecentForDownloadId(string downloadId)
        {
            return _historyRepository.MostRecentForDownloadId(downloadId);
        }

        public EpisodeHistory Get(int historyId)
        {
            return _historyRepository.Get(historyId);
        }

        public List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetBySeries(seriesId, eventType);
        }

        public List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetBySeason(seriesId, seasonNumber, eventType);
        }

        public List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.GetByEpisode(episodeId, eventType);
        }

        public List<EpisodeHistory> Find(string downloadId, EpisodeHistoryEventType eventType)
        {
            return _historyRepository.FindByDownloadId(downloadId).Where(c => c.EventType == eventType).ToList();
        }

        public List<EpisodeHistory> FindByDownloadId(string downloadId)
        {
            return _historyRepository.FindByDownloadId(downloadId);
        }

        public string FindDownloadId(EpisodeImportedEvent trackedDownload)
        {
            _logger.Debug("Trying to find downloadId for {0} from history", trackedDownload.ImportedEpisode.Path);

            var episodeIds = trackedDownload.EpisodeInfo.Episodes.Select(c => c.Id).ToList();
            var allHistory = _historyRepository.FindDownloadHistory(trackedDownload.EpisodeInfo.Series.Id, trackedDownload.ImportedEpisode.Quality);

            // Find download related items for these episodes
            var episodesHistory = allHistory.Where(h => episodeIds.Contains(h.EpisodeId)).ToList();

            var processedDownloadId = episodesHistory
                .Where(c => c.EventType != EpisodeHistoryEventType.Grabbed && c.DownloadId != null)
                .Select(c => c.DownloadId);

            var stillDownloading = episodesHistory.Where(c => c.EventType == EpisodeHistoryEventType.Grabbed && !processedDownloadId.Contains(c.DownloadId)).ToList();

            string downloadId = null;

            if (stillDownloading.Any())
            {
                foreach (var matchingHistory in trackedDownload.EpisodeInfo.Episodes.Select(e => stillDownloading.Where(c => c.EpisodeId == e.Id).ToList()))
                {
                    if (matchingHistory.Count != 1)
                    {
                        return null;
                    }

                    var newDownloadId = matchingHistory.Single().DownloadId;

                    if (downloadId == null || downloadId == newDownloadId)
                    {
                        downloadId = newDownloadId;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return downloadId;
        }

        public void Handle(EpisodeGrabbedEvent message)
        {
            foreach (var episode in message.Episode.Episodes)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Date = DateTime.UtcNow,
                    Quality = message.Episode.ParsedEpisodeInfo.Quality,
                    SourceTitle = message.Episode.Release.Title,
                    SeriesId = episode.SeriesId,
                    EpisodeId = episode.Id,
                    DownloadId = message.DownloadId,
                    Languages = message.Episode.Languages,
                };

                history.Data.Add("Indexer", message.Episode.Release.Indexer);
                history.Data.Add("NzbInfoUrl", message.Episode.Release.InfoUrl);
                history.Data.Add("ReleaseGroup", message.Episode.ParsedEpisodeInfo.ReleaseGroup);
                history.Data.Add("Age", message.Episode.Release.Age.ToString());
                history.Data.Add("AgeHours", message.Episode.Release.AgeHours.ToString());
                history.Data.Add("AgeMinutes", message.Episode.Release.AgeMinutes.ToString());
                history.Data.Add("PublishedDate", message.Episode.Release.PublishDate.ToUniversalTime().ToString("s") + "Z");
                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.DownloadClientName);
                history.Data.Add("Size", message.Episode.Release.Size.ToString());
                history.Data.Add("DownloadUrl", message.Episode.Release.DownloadUrl);
                history.Data.Add("Guid", message.Episode.Release.Guid);
                history.Data.Add("TvdbId", message.Episode.Release.TvdbId.ToString());
                history.Data.Add("TvRageId", message.Episode.Release.TvRageId.ToString());
                history.Data.Add("ImdbId", message.Episode.Release.ImdbId);
                history.Data.Add("Protocol", ((int)message.Episode.Release.DownloadProtocol).ToString());
                history.Data.Add("CustomFormatScore", message.Episode.CustomFormatScore.ToString());
                history.Data.Add("SeriesMatchType", message.Episode.SeriesMatchType.ToString());
                history.Data.Add("ReleaseSource", message.Episode.ReleaseSource.ToString());
                history.Data.Add("IndexerFlags", message.Episode.Release.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.Episode.ParsedEpisodeInfo.ReleaseType.ToString());

                if (!message.Episode.ParsedEpisodeInfo.ReleaseHash.IsNullOrWhiteSpace())
                {
                    history.Data.Add("ReleaseHash", message.Episode.ParsedEpisodeInfo.ReleaseHash);
                }

                if (message.Episode.Release is TorrentInfo torrentRelease)
                {
                    history.Data.Add("TorrentInfoHash", torrentRelease.InfoHash);
                }

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeImportedEvent message)
        {
            if (!message.NewDownload)
            {
                return;
            }

            var downloadId = message.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                downloadId = FindDownloadId(message);
            }

            foreach (var episode in message.EpisodeInfo.Episodes)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.DownloadFolderImported,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeInfo.Quality,
                    SourceTitle = message.ImportedEpisode.SceneName ?? Path.GetFileNameWithoutExtension(message.EpisodeInfo.Path),
                    SeriesId = message.ImportedEpisode.SeriesId,
                    EpisodeId = episode.Id,
                    DownloadId = downloadId,
                    Languages = message.EpisodeInfo.Languages
                };

                history.Data.Add("FileId", message.ImportedEpisode.Id.ToString());
                history.Data.Add("DroppedPath", message.EpisodeInfo.Path);
                history.Data.Add("ImportedPath", Path.Combine(message.EpisodeInfo.Series.Path, message.ImportedEpisode.RelativePath));
                history.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
                history.Data.Add("ReleaseGroup", message.EpisodeInfo.ReleaseGroup);
                history.Data.Add("CustomFormatScore", message.EpisodeInfo.CustomFormatScore.ToString());
                history.Data.Add("Size", message.EpisodeInfo.Size.ToString());
                history.Data.Add("IndexerFlags", message.ImportedEpisode.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.ImportedEpisode.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(DownloadFailedEvent message)
        {
            if (message.MovieId > 0)
            {
                var movieHistory = new MovieHistory
                {
                    EventType = MovieHistoryEventType.DownloadFailed,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    Languages = message.Languages,
                    SourceTitle = message.SourceTitle,
                    MovieId = message.MovieId,
                    DownloadId = message.DownloadId
                };

                movieHistory.Data.Add("DownloadClient", message.DownloadClient);
                movieHistory.Data.Add("DownloadClientName", message.TrackedDownload?.DownloadItem.DownloadClientInfo.Name);
                movieHistory.Data.Add("Message", message.Message);
                movieHistory.Data.Add("Source", message.Source);
                movieHistory.Data.Add("ReleaseGroup", message.TrackedDownload?.RemoteMovie?.ParsedMovieInfo?.ReleaseGroup ?? message.Data.GetValueOrDefault(MovieHistory.RELEASE_GROUP));
                movieHistory.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString() ?? message.Data.GetValueOrDefault(MovieHistory.SIZE));
                movieHistory.Data.Add("Indexer", message.TrackedDownload?.RemoteMovie?.Release?.Indexer ?? message.Data.GetValueOrDefault(MovieHistory.INDEXER));

                _historyRepository.Insert(movieHistory);

                return;
            }

            foreach (var episodeId in message.EpisodeIds)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.DownloadFailed,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    SourceTitle = message.SourceTitle,
                    SeriesId = message.SeriesId,
                    EpisodeId = episodeId,
                    DownloadId = message.DownloadId,
                    Languages = message.Languages
                };

                history.Data.Add("DownloadClient", message.DownloadClient);
                history.Data.Add("DownloadClientName", message.TrackedDownload?.DownloadItem.DownloadClientInfo.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("Source", message.Source);
                history.Data.Add("ReleaseGroup", message.TrackedDownload?.RemoteEpisode?.ParsedEpisodeInfo?.ReleaseGroup ?? message.Data.GetValueOrDefault(EpisodeHistory.RELEASE_GROUP));
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString() ?? message.Data.GetValueOrDefault(EpisodeHistory.SIZE));
                history.Data.Add("Indexer", message.TrackedDownload?.RemoteEpisode?.Release?.Indexer ?? message.Data.GetValueOrDefault(EpisodeHistory.INDEXER));

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing episode file from DB as part of cleanup routine, not creating history event.");
                return;
            }
            else if (message.Reason == DeleteMediaFileReason.ManualOverride)
            {
                _logger.Debug("Removing episode file from DB as part of manual override of existing file, not creating history event.");
                return;
            }

            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.EpisodeFileDeleted,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeFile.Quality,
                    SourceTitle = message.EpisodeFile.Path,
                    SeriesId = message.EpisodeFile.SeriesId,
                    EpisodeId = episode.Id,
                    Languages = message.EpisodeFile.Languages
                };

                history.Data.Add("Reason", message.Reason.ToString());
                history.Data.Add("ReleaseGroup", message.EpisodeFile.ReleaseGroup);
                history.Data.Add("Size", message.EpisodeFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.EpisodeFile.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.EpisodeFile.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(EpisodeFileRenamedEvent message)
        {
            var sourcePath = message.OriginalPath;
            var sourceRelativePath = message.Series.Path.GetRelativePath(message.OriginalPath);
            var path = Path.Combine(message.Series.Path, message.EpisodeFile.RelativePath);
            var relativePath = message.EpisodeFile.RelativePath;

            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.EpisodeFileRenamed,
                    Date = DateTime.UtcNow,
                    Quality = message.EpisodeFile.Quality,
                    SourceTitle = message.OriginalPath,
                    SeriesId = message.EpisodeFile.SeriesId,
                    EpisodeId = episode.Id,
                    Languages = message.EpisodeFile.Languages
                };

                history.Data.Add("SourcePath", sourcePath);
                history.Data.Add("SourceRelativePath", sourceRelativePath);
                history.Data.Add("Path", path);
                history.Data.Add("RelativePath", relativePath);
                history.Data.Add("ReleaseGroup", message.EpisodeFile.ReleaseGroup);
                history.Data.Add("Size", message.EpisodeFile.Size.ToString());
                history.Data.Add("IndexerFlags", message.EpisodeFile.IndexerFlags.ToString());
                history.Data.Add("ReleaseType", message.EpisodeFile.ReleaseType.ToString());

                _historyRepository.Insert(history);
            }
        }

        public void Handle(DownloadIgnoredEvent message)
        {
            if (message.MovieId > 0)
            {
                var movieHistory = new MovieHistory
                {
                    EventType = MovieHistoryEventType.DownloadIgnored,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    SourceTitle = message.SourceTitle,
                    MovieId = message.MovieId,
                    DownloadId = message.DownloadId,
                    Languages = message.Languages
                };

                movieHistory.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
                movieHistory.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
                movieHistory.Data.Add("Message", message.Message);
                movieHistory.Data.Add("ReleaseGroup", message.TrackedDownload?.RemoteMovie?.ParsedMovieInfo?.ReleaseGroup);
                movieHistory.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString());
                movieHistory.Data.Add("Indexer", message.TrackedDownload?.RemoteMovie?.Release?.Indexer);

                _historyRepository.Insert(movieHistory);

                return;
            }

            var historyToAdd = new List<EpisodeHistory>();

            foreach (var episodeId in message.EpisodeIds)
            {
                var history = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.DownloadIgnored,
                    Date = DateTime.UtcNow,
                    Quality = message.Quality,
                    SourceTitle = message.SourceTitle,
                    SeriesId = message.SeriesId,
                    EpisodeId = episodeId,
                    DownloadId = message.DownloadId,
                    Languages = message.Languages
                };

                history.Data.Add("DownloadClient", message.DownloadClientInfo.Type);
                history.Data.Add("DownloadClientName", message.DownloadClientInfo.Name);
                history.Data.Add("Message", message.Message);
                history.Data.Add("ReleaseGroup", message.TrackedDownload?.RemoteEpisode?.ParsedEpisodeInfo?.ReleaseGroup);
                history.Data.Add("Size", message.TrackedDownload?.DownloadItem.TotalSize.ToString());
                history.Data.Add("Indexer", message.TrackedDownload?.RemoteEpisode?.Release?.Indexer);
                history.Data.Add("ReleaseType", message.TrackedDownload?.RemoteEpisode?.ParsedEpisodeInfo?.ReleaseType.ToString());

                historyToAdd.Add(history);
            }

            _historyRepository.InsertMany(historyToAdd);
        }

        public void Handle(SeriesDeletedEvent message)
        {
            _historyRepository.DeleteForSeries(message.Series.Select(m => m.Id).ToList());
        }

        public void Handle(MovieGrabbedEvent message)
        {
            var history = new MovieHistory
            {
                EventType = MovieHistoryEventType.Grabbed,
                Date = DateTime.UtcNow,
                Quality = message.Movie.ParsedMovieInfo.Quality,
                Languages = message.Movie.Languages,
                SourceTitle = message.Movie.Release.Title,
                DownloadId = message.DownloadId,
                MovieId = message.Movie.Movie.Id
            };

            history.Data.Add("Indexer", message.Movie.Release.Indexer);
            history.Data.Add("NzbInfoUrl", message.Movie.Release.InfoUrl);
            history.Data.Add("ReleaseGroup", message.Movie.ParsedMovieInfo.ReleaseGroup);
            history.Data.Add("Age", message.Movie.Release.Age.ToString());
            history.Data.Add("AgeHours", message.Movie.Release.AgeHours.ToString());
            history.Data.Add("AgeMinutes", message.Movie.Release.AgeMinutes.ToString());
            history.Data.Add("PublishedDate", message.Movie.Release.PublishDate.ToUniversalTime().ToString("s") + "Z");
            history.Data.Add("DownloadClient", message.DownloadClient);
            history.Data.Add("DownloadClientName", message.DownloadClientName);
            history.Data.Add("Size", message.Movie.Release.Size.ToString());
            history.Data.Add("DownloadUrl", message.Movie.Release.DownloadUrl);
            history.Data.Add("Guid", message.Movie.Release.Guid);
            history.Data.Add("TmdbId", message.Movie.Release.TmdbId.ToString());
            history.Data.Add("ImdbId", message.Movie.Release.ImdbId);
            history.Data.Add("Protocol", ((int)message.Movie.Release.DownloadProtocol).ToString());
            history.Data.Add("CustomFormatScore", message.Movie.CustomFormatScore.ToString());
            history.Data.Add("MovieMatchType", message.Movie.MovieMatchType.ToString());
            history.Data.Add("ReleaseSource", message.Movie.ReleaseSource.ToString());
            history.Data.Add("IndexerFlags", message.Movie.Release.IndexerFlags.ToString());
            history.Data.Add("IndexerId", message.Movie.Release.IndexerId.ToString());

            if (!message.Movie.ParsedMovieInfo.ReleaseHash.IsNullOrWhiteSpace())
            {
                history.Data.Add("ReleaseHash", message.Movie.ParsedMovieInfo.ReleaseHash);
            }

            if (message.Movie.Release is TorrentInfo torrentRelease)
            {
                history.Data.Add("TorrentInfoHash", torrentRelease.InfoHash);
            }

            _historyRepository.Insert(history);
        }

        public void Handle(MovieFileImportedEvent message)
        {
            if (!message.NewDownload)
            {
                return;
            }

            var downloadId = message.DownloadId;

            if (downloadId.IsNullOrWhiteSpace())
            {
                downloadId = FindDownloadId(message);
            }

            var movie = message.MovieInfo.Movie;
            var history = new MovieHistory
            {
                EventType = MovieHistoryEventType.DownloadFolderImported,
                Date = DateTime.UtcNow,
                Quality = message.MovieInfo.Quality,
                Languages = message.MovieInfo.Languages,
                SourceTitle = message.ImportedMovie.SceneName ?? Path.GetFileNameWithoutExtension(message.MovieInfo.Path),
                DownloadId = downloadId,
                MovieId = movie.Id,
            };

            history.Data.Add("FileId", message.ImportedMovie.Id.ToString());
            history.Data.Add("DroppedPath", message.MovieInfo.Path);
            history.Data.Add("ImportedPath", Path.Combine(movie.Path, message.ImportedMovie.RelativePath));
            history.Data.Add("DownloadClient", message.DownloadClientInfo?.Type);
            history.Data.Add("DownloadClientName", message.DownloadClientInfo?.Name);
            history.Data.Add("ReleaseGroup", message.MovieInfo.ReleaseGroup);
            history.Data.Add("CustomFormatScore", message.MovieInfo.CustomFormatScore.ToString());
            history.Data.Add("Size", message.MovieInfo.Size.ToString());
            history.Data.Add("IndexerFlags", message.ImportedMovie.IndexerFlags.ToString());

            _historyRepository.Insert(history);
        }

        public void Handle(MovieFileDeletedEvent message)
        {
            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing movie file from DB as part of cleanup routine, not creating history event.");
                return;
            }

            var history = new MovieHistory
            {
                EventType = MovieHistoryEventType.MovieFileDeleted,
                Date = DateTime.UtcNow,
                Quality = message.MovieFile.Quality,
                Languages = message.MovieFile.Languages,
                SourceTitle = message.MovieFile.Path,
                MovieId = message.MovieFile.MovieId
            };

            history.Data.Add("Reason", message.Reason.ToString());
            history.Data.Add("ReleaseGroup", message.MovieFile.ReleaseGroup);
            history.Data.Add("Size", message.MovieFile.Size.ToString());
            history.Data.Add("IndexerFlags", message.MovieFile.IndexerFlags.ToString());

            _historyRepository.Insert(history);
        }

        public void Handle(MovieFileRenamedEvent message)
        {
            var sourcePath = message.OriginalPath;
            var sourceRelativePath = message.Movie.Path.GetRelativePath(message.OriginalPath);
            var path = Path.Combine(message.Movie.Path, message.MovieFile.RelativePath);
            var relativePath = message.MovieFile.RelativePath;

            var history = new MovieHistory
            {
                EventType = MovieHistoryEventType.MovieFileRenamed,
                Date = DateTime.UtcNow,
                Quality = message.MovieFile.Quality,
                Languages = message.MovieFile.Languages,
                SourceTitle = message.OriginalPath,
                MovieId = message.MovieFile.MovieId,
            };

            history.Data.Add("SourcePath", sourcePath);
            history.Data.Add("SourceRelativePath", sourceRelativePath);
            history.Data.Add("Path", path);
            history.Data.Add("RelativePath", relativePath);
            history.Data.Add("ReleaseGroup", message.MovieFile.ReleaseGroup);
            history.Data.Add("Size", message.MovieFile.Size.ToString());
            history.Data.Add("IndexerFlags", message.MovieFile.IndexerFlags.ToString());

            _historyRepository.Insert(history);
        }

        public void Handle(MoviesDeletedEvent message)
        {
            _historyRepository.DeleteForMovies(message.Movies.Select(m => m.Id).ToList());
        }

        public List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType)
        {
            return _historyRepository.Since(date, eventType);
        }

        public QualityModel GetBestQualityInHistory(QualityProfile profile, int movieId)
        {
            var comparer = new QualityModelComparer(profile);

            return _historyRepository.GetBestQualityInHistory(movieId).MaxBy(q => q, comparer);
        }

        public MovieHistory MostRecentForMovie(int movieId)
        {
            return _historyRepository.MostRecentForMovie(movieId);
        }

        public List<MovieHistory> FindByMovieId(int movieId)
        {
            return _historyRepository.FindByMovieId(movieId);
        }

        public MovieHistory GetMovie(int historyId)
        {
            return _historyRepository.GetMovie(historyId);
        }

        public List<MovieHistory> FindMovieByDownloadId(string downloadId)
        {
            return _historyRepository.FindMovieByDownloadId(downloadId);
        }

        public List<MovieHistory> FindMovie(string downloadId, MovieHistoryEventType eventType)
        {
            return _historyRepository.FindMovieByDownloadId(downloadId).Where(c => c.EventType == eventType).ToList();
        }

        public List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType)
        {
            return _historyRepository.GetByMovieId(movieId, eventType);
        }

        public void UpdateMany(List<MovieHistory> toUpdate)
        {
            _historyRepository.UpdateMany(toUpdate);
        }

        public string FindDownloadId(MovieFileImportedEvent trackedDownload)
        {
            _logger.Debug("Trying to find downloadId for {0} from history", trackedDownload.ImportedMovie.Path);

            var movieId = trackedDownload.MovieInfo.Movie.Id;
            var movieHistory = _historyRepository.FindMovieDownloadHistory(movieId, trackedDownload.ImportedMovie.Quality);

            var processedDownloadId = movieHistory
                .Where(c => c.EventType != MovieHistoryEventType.Grabbed && c.DownloadId != null)
                .Select(c => c.DownloadId);

            var stillDownloading = movieHistory.Where(c => c.EventType == MovieHistoryEventType.Grabbed && !processedDownloadId.Contains(c.DownloadId)).ToList();

            string downloadId = null;

            if (stillDownloading.Any())
            {
                if (stillDownloading.Count != 1)
                {
                    return null;
                }

                downloadId = stillDownloading.Single().DownloadId;
            }

            return downloadId;
        }

        public List<MovieHistory> SinceMovie(DateTime date, MovieHistoryEventType? eventType)
        {
            return _historyRepository.SinceMovie(date, eventType);
        }
    }
}
