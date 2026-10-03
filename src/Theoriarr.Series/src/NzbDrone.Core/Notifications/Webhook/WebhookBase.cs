using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Webhook
{
    public abstract class WebhookBase<TSettings> : NotificationBase<TSettings>
        where TSettings : NotificationSettingsBase<TSettings>, new()
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        protected readonly ILocalizationService _localizationService;
        private readonly ITagRepository _tagRepository;
        private readonly IMapCoversToLocal _mediaCoverService;

        protected WebhookBase(IConfigFileProvider configFileProvider, IConfigService configService, ILocalizationService localizationService, ITagRepository tagRepository, IMapCoversToLocal mediaCoverService)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _localizationService = localizationService;
            _tagRepository = tagRepository;
            _mediaCoverService = mediaCoverService;
        }

        protected WebhookPayload BuildOnGrabPayload(GrabMessage message)
        {
            if (message.Movie != null)
            {
                var remoteMovie = message.RemoteMovie;
                var movieQuality = message.Quality;

                return new WebhookGrabPayload
                {
                    EventType = WebhookEventType.Grab,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Movie = GetMovie(message.Movie),
                    RemoteMovie = new WebhookRemoteMovie(remoteMovie),
                    Release = new WebhookRelease(movieQuality, remoteMovie),
                    DownloadClient = message.DownloadClientName,
                    DownloadClientType = message.DownloadClientType,
                    DownloadId = message.DownloadId,
                    CustomFormatInfo = new WebhookCustomFormatInfo(remoteMovie.CustomFormats, remoteMovie.CustomFormatScore)
                };
            }
            else
            {
                var remoteEpisode = message.Episode;
                var episodeQuality = message.Quality;

                return new WebhookGrabPayload
                {
                    EventType = WebhookEventType.Grab,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Series = GetSeries(message.Series),
                    Episodes = remoteEpisode.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                    Release = new WebhookRelease(episodeQuality, remoteEpisode),
                    DownloadClient = message.DownloadClientName,
                    DownloadClientType = message.DownloadClientType,
                    DownloadId = message.DownloadId,
                    CustomFormatInfo = new WebhookCustomFormatInfo(remoteEpisode.CustomFormats, remoteEpisode.CustomFormatScore),
                };
            }
        }

        protected WebhookImportPayload BuildOnDownloadPayload(DownloadMessage message)
        {
            if (message.Movie != null)
            {
                var movieFile = message.MovieFile;

                var moviePayload = new WebhookImportPayload
                {
                    EventType = WebhookEventType.Download,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Movie = GetMovie(message.Movie),
                    RemoteMovie = new WebhookRemoteMovie(message.Movie),
                    MovieFile = new WebhookMovieFile(movieFile)
                    {
                        SourcePath = message.SourcePath
                    },
                    Release = new WebhookGrabbedRelease(message.Release, movieFile.IndexerFlags),
                    IsUpgrade = message.OldMovieFiles.Any(),
                    DownloadClient = message.DownloadClientInfo?.Name,
                    DownloadClientType = message.DownloadClientInfo?.Type,
                    DownloadId = message.DownloadId,
                    CustomFormatInfo = new WebhookCustomFormatInfo(message.MovieInfo.CustomFormats, message.MovieInfo.CustomFormatScore)
                };

                if (message.OldMovieFiles.Any())
                {
                    moviePayload.DeletedFiles = message.OldMovieFiles.ConvertAll(x =>
                        new WebhookMovieFile(x.MovieFile)
                        {
                            Path = Path.Combine(message.Movie.Path, x.MovieFile.RelativePath),
                            RecycleBinPath = x.RecycleBinPath
                        });
                }

                return moviePayload;
            }
            else
            {
                var episodeFile = message.EpisodeFile;

                var payload = new WebhookImportPayload
                {
                    EventType = WebhookEventType.Download,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Series = GetSeries(message.Series),
                    Episodes = episodeFile.Episodes.Value.ConvertAll(x => new WebhookEpisode(x)),
                    EpisodeFile = new WebhookEpisodeFile(episodeFile)
                    {
                        SourcePath = message.SourcePath
                    },
                    Release = new WebhookGrabbedRelease(message.Release, episodeFile.IndexerFlags, episodeFile.ReleaseType),
                    IsUpgrade = message.OldFiles.Any(),
                    DownloadClient = message.DownloadClientInfo?.Name,
                    DownloadClientType = message.DownloadClientInfo?.Type,
                    DownloadId = message.DownloadId,
                    CustomFormatInfo = new WebhookCustomFormatInfo(message.EpisodeInfo.CustomFormats, message.EpisodeInfo.CustomFormatScore)
                };

                if (message.OldFiles.Any())
                {
                    payload.DeletedFiles = message.OldFiles.ConvertAll(x => new WebhookEpisodeFile(x.EpisodeFile)
                    {
                        Path = Path.Combine(message.Series.Path, x.EpisodeFile.RelativePath),
                        RecycleBinPath = x.RecycleBinPath
                    });
                }

                return payload;
            }
        }

        protected WebhookImportCompletePayload BuildOnImportCompletePayload(ImportCompleteMessage message)
        {
            var episodeFiles = message.EpisodeFiles;

            var payload = new WebhookImportCompletePayload
            {
                EventType = WebhookEventType.Download,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = GetSeries(message.Series),
                Episodes = message.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                EpisodeFiles = episodeFiles.ConvertAll(e => new WebhookEpisodeFile(e)),
                Release = new WebhookGrabbedRelease(message.Release, episodeFiles.First().IndexerFlags, episodeFiles.First().ReleaseType),
                DownloadClient = message.DownloadClientInfo?.Name,
                DownloadClientType = message.DownloadClientInfo?.Type,
                DownloadId = message.DownloadId,
                SourcePath = message.SourcePath,
                DestinationPath = message.DestinationPath
            };

            return payload;
        }

        protected WebhookEpisodeDeletePayload BuildOnEpisodeFileDelete(EpisodeDeleteMessage deleteMessage)
        {
            return new WebhookEpisodeDeletePayload
            {
                EventType = WebhookEventType.EpisodeFileDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = GetSeries(deleteMessage.Series),
                Episodes = deleteMessage.EpisodeFile.Episodes.Value.ConvertAll(x => new WebhookEpisode(x)),
                EpisodeFile = new WebhookEpisodeFile(deleteMessage.EpisodeFile),
                DeleteReason = deleteMessage.Reason
            };
        }

        protected WebhookSeriesAddPayload BuildOnSeriesAdd(SeriesAddMessage addMessage)
        {
            return new WebhookSeriesAddPayload
            {
                EventType = WebhookEventType.SeriesAdd,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = GetSeries(addMessage.Series),
            };
        }

        protected WebhookSeriesDeletePayload BuildOnSeriesDelete(SeriesDeleteMessage deleteMessage)
        {
            return new WebhookSeriesDeletePayload
            {
                EventType = WebhookEventType.SeriesDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = GetSeries(deleteMessage.Series),
                DeletedFiles = deleteMessage.DeletedFiles
            };
        }

        protected WebhookAddedPayload BuildOnMovieAdded(Movie movie)
        {
            return new WebhookAddedPayload
            {
                EventType = WebhookEventType.MovieAdded,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Movie = GetMovie(movie),
                AddMethod = movie.AddOptions.AddMethod
            };
        }

        protected WebhookMovieFileDeletePayload BuildOnMovieFileDelete(MovieFileDeleteMessage deleteMessage)
        {
            return new WebhookMovieFileDeletePayload
            {
                EventType = WebhookEventType.MovieFileDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Movie = GetMovie(deleteMessage.Movie),
                MovieFile = new WebhookMovieFile(deleteMessage.MovieFile),
                DeleteReason = deleteMessage.Reason
            };
        }

        protected WebhookMovieDeletePayload BuildOnMovieDelete(MovieDeleteMessage deleteMessage)
        {
            var payload = new WebhookMovieDeletePayload
            {
                EventType = WebhookEventType.MovieDelete,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Movie = GetMovie(deleteMessage.Movie),
                DeletedFiles = deleteMessage.DeletedFiles
            };

            if (deleteMessage.DeletedFiles && deleteMessage.Movie.MovieFile != null)
            {
                payload.MovieFolderSize = deleteMessage.Movie.MovieFile.Size;
            }

            return payload;
        }

        protected WebhookRenamePayload BuildOnRenamePayload(Series series, List<RenamedEpisodeFile> renamedFiles)
        {
            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = GetSeries(series),
                RenamedEpisodeFiles = renamedFiles.ConvertAll(x => new WebhookRenamedEpisodeFile(x))
            };
        }

        protected WebhookRenamePayload BuildOnRenamePayload(Movie movie, List<RenamedMovieFile> renamedFiles)
        {
            return new WebhookRenamePayload
            {
                EventType = WebhookEventType.Rename,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Movie = GetMovie(movie),
                RenamedMovieFiles = renamedFiles.ConvertAll(x => new WebhookRenamedMovieFile(x))
            };
        }

        protected WebhookHealthPayload BuildHealthPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.Health,
                InstanceName = _configFileProvider.InstanceName,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookHealthPayload BuildHealthRestoredPayload(HealthCheck.HealthCheck healthCheck)
        {
            return new WebhookHealthPayload
            {
                EventType = WebhookEventType.HealthRestored,
                InstanceName = _configFileProvider.InstanceName,
                Level = healthCheck.Type,
                Message = healthCheck.Message,
                Type = healthCheck.Source.Name,
                WikiUrl = healthCheck.WikiUrl?.ToString()
            };
        }

        protected WebhookApplicationUpdatePayload BuildApplicationUpdatePayload(ApplicationUpdateMessage updateMessage)
        {
            return new WebhookApplicationUpdatePayload
            {
                EventType = WebhookEventType.ApplicationUpdate,
                InstanceName = _configFileProvider.InstanceName,
                Message = updateMessage.Message,
                PreviousVersion = updateMessage.PreviousVersion.ToString(),
                NewVersion = updateMessage.NewVersion.ToString()
            };
        }

        protected WebhookManualInteractionPayload BuildManualInteractionRequiredPayload(ManualInteractionRequiredMessage message)
        {
            var quality = message.Quality;

            if (message.Movie != null)
            {
                var remoteMovie = message.RemoteMovie;

                return new WebhookManualInteractionPayload
                {
                    EventType = WebhookEventType.ManualInteractionRequired,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Movie = GetMovie(message.Movie),
                    DownloadInfo = new WebhookDownloadClientItem(quality, message.TrackedDownload.DownloadItem),
                    DownloadClient = message.DownloadClientInfo?.Name,
                    DownloadClientType = message.DownloadClientInfo?.Type,
                    DownloadId = message.DownloadId,
                    DownloadStatus = message.TrackedDownload.Status.ToString(),
                    DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList(),
                    CustomFormatInfo = new WebhookCustomFormatInfo(remoteMovie?.CustomFormats, remoteMovie?.CustomFormatScore ?? 0),
                    Release = new WebhookGrabbedRelease(message.Release)
                };
            }
            else
            {
                var remoteEpisode = message.Episode;

                return new WebhookManualInteractionPayload
                {
                    EventType = WebhookEventType.ManualInteractionRequired,
                    InstanceName = _configFileProvider.InstanceName,
                    ApplicationUrl = _configService.ApplicationUrl,
                    Series = GetSeries(message.Series),
                    Episodes = remoteEpisode.Episodes.ConvertAll(x => new WebhookEpisode(x)),
                    DownloadInfo = new WebhookDownloadClientItem(quality, message.TrackedDownload.DownloadItem),
                    DownloadClient = message.DownloadClientInfo?.Name,
                    DownloadClientType = message.DownloadClientInfo?.Type,
                    DownloadId = message.DownloadId,
                    DownloadStatus = message.TrackedDownload.Status.ToString(),
                    DownloadStatusMessages = message.TrackedDownload.StatusMessages.Select(x => new WebhookDownloadStatusMessage(x)).ToList(),
                    CustomFormatInfo = new WebhookCustomFormatInfo(remoteEpisode.CustomFormats, remoteEpisode.CustomFormatScore),
                    Release = new WebhookGrabbedRelease(message.Release)
                };
            }
        }

        protected WebhookPayload BuildTestPayload()
        {
            return new WebhookGrabPayload
            {
                EventType = WebhookEventType.Test,
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Series = new WebhookSeries
                {
                    Id = 1,
                    Title = "Test Title",
                    Path = "C:\\testpath",
                    TvdbId = 1234,
                    Tags = new List<string> { "test-tag" }
                },
                Episodes = new List<WebhookEpisode>
                {
                    new()
                    {
                        Id = 123,
                        EpisodeNumber = 1,
                        SeasonNumber = 1,
                        Title = "Test title"
                    }
                }
            };
        }

        private WebhookSeries GetSeries(Series series)
        {
            if (series == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(series.Id, series.Images, series.Added);

            return new WebhookSeries(series, GetTagLabels(series));
        }

        private WebhookMovie GetMovie(Movie movie)
        {
            if (movie == null)
            {
                return null;
            }

            _mediaCoverService.ConvertToLocalUrls(movie.Id, movie.MovieMetadata.Value.Images);

            return new WebhookMovie(movie, GetTagLabels(movie));
        }

        private List<string> GetTagLabels(Series series)
        {
            if (series == null)
            {
                return null;
            }

            return _tagRepository.GetTags(series.Tags)
                .Select(s => s.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }

        private List<string> GetTagLabels(Movie movie)
        {
            if (movie == null)
            {
                return null;
            }

            return _tagRepository.GetTags(movie.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }
    }
}
