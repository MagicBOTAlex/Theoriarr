using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Emby
{
    public class MediaBrowser : NotificationBase<MediaBrowserSettings>
    {
        private readonly IMediaBrowserService _mediaBrowserService;
        private readonly MediaServerUpdateQueue<MediaBrowser, string> _updateQueue;
        private readonly Logger _logger;

        private static string Created = "Created";
        private static string Deleted = "Deleted";
        private static string Modified = "Modified";

        public MediaBrowser(IMediaBrowserService mediaBrowserService, ICacheManager cacheManager, Logger logger)
        {
            _mediaBrowserService = mediaBrowserService;
            _updateQueue = new MediaServerUpdateQueue<MediaBrowser, string>(cacheManager);
            _logger = logger;
        }

        public override string Link => "https://emby.media/";
        public override string Name => "Emby / Jellyfin";

        public override void OnGrab(GrabMessage grabMessage)
        {
            if (Settings.Notify)
            {
                var title = grabMessage.Movie != null ? MOVIE_GRABBED_TITLE_BRANDED : EPISODE_GRABBED_TITLE_BRANDED;

                _mediaBrowserService.Notify(Settings, title, grabMessage.Message);
            }
        }

        public override void OnDownload(DownloadMessage message)
        {
            if (message.Movie != null)
            {
                if (Settings.Notify)
                {
                    _mediaBrowserService.Notify(Settings, MOVIE_DOWNLOADED_TITLE_BRANDED, message.Message);
                }

                UpdateIfEnabled(message.Movie, Created);
            }
            else
            {
                if (Settings.Notify)
                {
                    _mediaBrowserService.Notify(Settings, EPISODE_DOWNLOADED_TITLE_BRANDED, message.Message);
                }

                UpdateIfEnabled(message.Series, Created);
            }
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, IMPORT_COMPLETE_TITLE_BRANDED, message.Message);
            }

            UpdateIfEnabled(message.Series, Created);
        }

        public override void OnRename(Series series, List<RenamedEpisodeFile> renamedFiles)
        {
            UpdateIfEnabled(series, Modified);
        }

        public override void OnMovieRename(Movie movie, List<RenamedMovieFile> renamedFiles)
        {
            UpdateIfEnabled(movie, Modified);
        }

        public override void OnEpisodeFileDelete(EpisodeDeleteMessage deleteMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, EPISODE_DELETED_TITLE_BRANDED, deleteMessage.Message);
            }

            UpdateIfEnabled(deleteMessage.Series, Deleted);
        }

        public override void OnSeriesAdd(SeriesAddMessage message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, SERIES_ADDED_TITLE_BRANDED, message.Message);
            }

            UpdateIfEnabled(message.Series, Created);
        }

        public override void OnSeriesDelete(SeriesDeleteMessage deleteMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, SERIES_DELETED_TITLE_BRANDED, deleteMessage.Message);
            }

            UpdateIfEnabled(deleteMessage.Series, Deleted);
        }

        public override void OnMovieDelete(MovieDeleteMessage deleteMessage)
        {
            if (deleteMessage.DeletedFiles)
            {
                if (Settings.Notify)
                {
                    _mediaBrowserService.Notify(Settings, MOVIE_DELETED_TITLE_BRANDED, deleteMessage.Message);
                }

                UpdateIfEnabled(deleteMessage.Movie, Deleted);
            }
        }

        public override void OnMovieFileDelete(MovieFileDeleteMessage deleteMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, MOVIE_FILE_DELETED_TITLE_BRANDED, deleteMessage.Message);
            }

            UpdateIfEnabled(deleteMessage.Movie, Deleted);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck message)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, HEALTH_ISSUE_TITLE_BRANDED, message.Message);
            }
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousMessage.Message}");
            }
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            if (Settings.Notify)
            {
                _mediaBrowserService.Notify(Settings, APPLICATION_UPDATE_TITLE_BRANDED, updateMessage.Message);
            }
        }

        public override void ProcessQueue()
        {
            _updateQueue.ProcessQueue(Settings.Host, (items) =>
            {
                if (Settings.UpdateLibrary)
                {
                    _logger.Debug("Performing library update for {0} items", items.Count);

                    items.ForEach(item =>
                    {
                        // If there is only one update type use that, otherwise send null and let Emby decide
                        var updateType = item.Info.Count == 1 ? item.Info.First() : null;

                        if (item.Movie != null)
                        {
                            _mediaBrowserService.Update(Settings, item.Movie, updateType);
                        }
                        else
                        {
                            _mediaBrowserService.Update(Settings, item.Series, updateType);
                        }
                    });
                }
            });
        }

        private void UpdateIfEnabled(Series series, string updateType)
        {
            if (Settings.UpdateLibrary)
            {
                _logger.Debug("Scheduling library update for series {0} {1}", series.Id, series.Title);
                _updateQueue.Add(Settings.Host, series, updateType);
            }
        }

        private void UpdateIfEnabled(Movie movie, string updateType)
        {
            if (Settings.UpdateLibrary)
            {
                _logger.Debug("Scheduling library update for movie {0} {1}", movie.Id, movie.Title);
                _updateQueue.Add(Settings.Host, movie, updateType);
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_mediaBrowserService.Test(Settings));

            return new ValidationResult(failures);
        }
    }
}
