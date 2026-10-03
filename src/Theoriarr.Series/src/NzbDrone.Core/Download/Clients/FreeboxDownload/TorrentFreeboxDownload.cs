using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Clients.FreeboxDownload.Responses;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.FreeboxDownload
{
    public class TorrentFreeboxDownload : TorrentClientBase<FreeboxDownloadSettings>
    {
        private readonly IFreeboxDownloadProxy _proxy;

        public TorrentFreeboxDownload(IFreeboxDownloadProxy proxy,
            ITorrentFileInfoReader torrentFileInfoReader,
            IHttpClient httpClient,
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            IBlocklistService blocklistService,
            Logger logger)
            : base(torrentFileInfoReader, httpClient, configService, diskProvider, remotePathMappingService, localizationService, blocklistService, logger)
        {
            _proxy = proxy;
        }

        public override string Name => "Freebox Download";

        protected IEnumerable<FreeboxDownloadTask> GetTorrents()
        {
            return _proxy.GetTasks(Settings).Where(v => v.Type.ToLower() == FreeboxDownloadTaskType.Bt.ToString().ToLower());
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var torrents = GetTorrents();

            var queueItems = new List<DownloadClientItem>();

            foreach (var torrent in torrents)
            {
                var outputPath = new OsPath(torrent.DecodedDownloadDirectory);

                var tvConfigured = Settings.DestinationDirectory.IsNotNullOrWhiteSpace() || Settings.Category.IsNotNullOrWhiteSpace();
                var movieConfigured = Settings.MovieDirectory.IsNotNullOrWhiteSpace() || Settings.MovieCategory.IsNotNullOrWhiteSpace();

                var tvMatch = !tvConfigured || MatchesCategoryOrDirectory(outputPath, Settings.DestinationDirectory, Settings.Category);
                var movieMatch = movieConfigured && MatchesCategoryOrDirectory(outputPath, Settings.MovieDirectory, Settings.MovieCategory);

                if (!tvMatch && !movieMatch)
                {
                    continue;
                }

                var category = tvMatch && tvConfigured ? Settings.Category :
                    movieMatch && movieConfigured ? Settings.MovieCategory : null;

                var item = new DownloadClientItem()
                {
                    DownloadId = torrent.Id,
                    Category = category,
                    Title = torrent.Name,
                    TotalSize = torrent.Size,
                    DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false),
                    RemainingSize = (long)(torrent.Size * (double)(1 - ((double)torrent.ReceivedPrct / 10000))),
                    RemainingTime = torrent.Eta <= 0 ? null : TimeSpan.FromSeconds(torrent.Eta),
                    SeedRatio = torrent.StopRatio <= 0 ? 0 : torrent.StopRatio / 100,
                    OutputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, outputPath)
                };

                switch (torrent.Status)
                {
                    case FreeboxDownloadTaskStatus.Stopped: // task is stopped, can be resumed by setting the status to downloading
                    case FreeboxDownloadTaskStatus.Stopping: // task is gracefully stopping
                        item.Status = DownloadItemStatus.Paused;
                        break;

                    case FreeboxDownloadTaskStatus.Queued: // task will start when a new download slot is available the queue position is stored in queue_pos attribute
                        item.Status = DownloadItemStatus.Queued;
                        break;

                    case FreeboxDownloadTaskStatus.Starting: // task is preparing to start download
                    case FreeboxDownloadTaskStatus.Downloading:
                    case FreeboxDownloadTaskStatus.Retry: // you can set a task status to ‘retry’ to restart the download task.
                    case FreeboxDownloadTaskStatus.Checking: // checking data before launching download.
                        item.Status = DownloadItemStatus.Downloading;
                        break;

                    case FreeboxDownloadTaskStatus.Error: // there was a problem with the download, you can get an error code in the error field
                        item.Status = DownloadItemStatus.Warning;
                        item.Message = torrent.GetErrorDescription();
                        break;

                    case FreeboxDownloadTaskStatus.Done: // the download is over. For bt you can resume seeding setting the status to seeding if the ratio is not reached yet
                    case FreeboxDownloadTaskStatus.Seeding: // download is over, the content is Change to being shared to other users. The task will automatically stop once the seed ratio has been reached
                        item.Status = DownloadItemStatus.Completed;
                        break;

                    case FreeboxDownloadTaskStatus.Unknown:
                    default: // new status in API? default to downloading
                        item.Message = _localizationService.GetLocalizedString("UnknownDownloadState",
                            new Dictionary<string, object> { { "state", torrent.Status } });
                        _logger.Info($"Unknown download state: {torrent.Status}");
                        item.Status = DownloadItemStatus.Downloading;
                        break;
                }

                item.CanBeRemoved = item.CanMoveFiles = item.DownloadClientInfo.RemoveCompletedDownloads && torrent.Status == FreeboxDownloadTaskStatus.Done;

                queueItems.Add(item);
            }

            return queueItems;
        }

        protected override string AddFromMagnetLink(RemoteEpisode remoteEpisode, string hash, string magnetLink)
        {
            return _proxy.AddTaskFromUrl(magnetLink,
                                         GetDownloadDirectory(remoteEpisode).EncodeBase64(),
                                         ToBePaused(),
                                         ToBeQueuedFirst(remoteEpisode),
                                         GetSeedRatio(remoteEpisode),
                                         Settings);
        }

        protected override string AddFromTorrentFile(RemoteEpisode remoteEpisode, string hash, string filename, byte[] fileContent)
        {
            return _proxy.AddTaskFromFile(filename,
                                          fileContent,
                                          GetDownloadDirectory(remoteEpisode).EncodeBase64(),
                                          ToBePaused(),
                                          ToBeQueuedFirst(remoteEpisode),
                                          GetSeedRatio(remoteEpisode),
                                          Settings);
        }

        protected override string AddFromMagnetLink(RemoteMovie remoteMovie, string hash, string magnetLink)
        {
            return _proxy.AddTaskFromUrl(magnetLink,
                                         GetMovieDownloadDirectory(remoteMovie).EncodeBase64(),
                                         ToBePaused(),
                                         ToBeQueuedFirst(remoteMovie),
                                         GetSeedRatio(remoteMovie),
                                         Settings);
        }

        protected override string AddFromTorrentFile(RemoteMovie remoteMovie, string hash, string filename, byte[] fileContent)
        {
            return _proxy.AddTaskFromFile(filename,
                                          fileContent,
                                          GetMovieDownloadDirectory(remoteMovie).EncodeBase64(),
                                          ToBePaused(),
                                          ToBeQueuedFirst(remoteMovie),
                                          GetSeedRatio(remoteMovie),
                                          Settings);
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            _proxy.DeleteTask(item.DownloadId, deleteData, Settings);
        }

        public override DownloadClientInfo GetStatus()
        {
            var destDir = GetDownloadDirectory();

            var outputRootFolders = new List<OsPath> { _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(destDir)) };

            var movieDestDir = GetMovieDownloadDirectory();

            if (movieDestDir.IsNotNullOrWhiteSpace() && movieDestDir != destDir)
            {
                outputRootFolders.Add(_remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(movieDestDir)));
            }

            return new DownloadClientInfo
            {
                IsLocalhost = Settings.Host.IsLocalhostAddress(),
                OutputRootFolders = outputRootFolders
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            try
            {
                _proxy.Authenticate(Settings);
            }
            catch (DownloadClientUnavailableException ex)
            {
                failures.Add(new ValidationFailure("Host", ex.Message));
                failures.Add(new ValidationFailure("Port", ex.Message));
            }
            catch (DownloadClientAuthenticationException ex)
            {
                failures.Add(new ValidationFailure("AppId", ex.Message));
                failures.Add(new ValidationFailure("AppToken", ex.Message));
            }
            catch (FreeboxDownloadException ex)
            {
                failures.Add(new ValidationFailure("ApiUrl", ex.Message));
            }
        }

        private string GetDownloadDirectory(RemoteEpisode remoteEpisode = null)
        {
            string destDir;

            if (Settings.DestinationDirectory.IsNotNullOrWhiteSpace())
            {
                destDir = Settings.DestinationDirectory.TrimEnd('/');
            }
            else
            {
                destDir = _proxy.GetDownloadConfiguration(Settings).DecodedDownloadDirectory.TrimEnd('/');

                if (Settings.Category.IsNotNullOrWhiteSpace())
                {
                    destDir = $"{destDir}/{Settings.Category}";
                }
            }

            if (remoteEpisode?.Release?.Title.IsNotNullOrWhiteSpace() ?? false)
            {
                var folderName = FileNameBuilder.CleanFileName(remoteEpisode.Release.Title);
                destDir = $"{destDir}/{folderName}";
            }

            return destDir;
        }

        private string GetMovieDownloadDirectory(RemoteMovie remoteMovie = null)
        {
            var directory = Settings.MovieDirectory.IsNotNullOrWhiteSpace() ? Settings.MovieDirectory : Settings.DestinationDirectory;
            var category = Settings.MovieCategory.IsNotNullOrWhiteSpace() ? Settings.MovieCategory : Settings.Category;

            string destDir;

            if (directory.IsNotNullOrWhiteSpace())
            {
                destDir = directory.TrimEnd('/');
            }
            else
            {
                destDir = _proxy.GetDownloadConfiguration(Settings).DecodedDownloadDirectory.TrimEnd('/');

                if (category.IsNotNullOrWhiteSpace())
                {
                    destDir = $"{destDir}/{category}";
                }
            }

            if (remoteMovie?.Release?.Title.IsNotNullOrWhiteSpace() ?? false)
            {
                var folderName = FileNameBuilder.CleanFileName(remoteMovie.Release.Title);
                destDir = $"{destDir}/{folderName}";
            }

            return destDir;
        }

        private bool MatchesCategoryOrDirectory(OsPath outputPath, string directory, string category)
        {
            if (directory.IsNotNullOrWhiteSpace())
            {
                return new OsPath(directory).Contains(outputPath);
            }

            if (category.IsNotNullOrWhiteSpace())
            {
                return outputPath.FullPath.Split('\\', '/').Contains(category);
            }

            return true;
        }

        private bool ToBePaused()
        {
            return Settings.AddPaused;
        }

        private bool ToBeQueuedFirst(RemoteEpisode remoteEpisode)
        {
            if ((remoteEpisode.IsRecentEpisode() && Settings.RecentPriority == (int)FreeboxDownloadPriority.First) ||
                (!remoteEpisode.IsRecentEpisode() && Settings.OlderPriority == (int)FreeboxDownloadPriority.First))
            {
                return true;
            }

            return false;
        }

        private double? GetSeedRatio(RemoteEpisode remoteEpisode)
        {
            if (remoteEpisode.SeedConfiguration == null || remoteEpisode.SeedConfiguration.Ratio == null)
            {
                return null;
            }

            return remoteEpisode.SeedConfiguration.Ratio.Value * 100;
        }

        private bool ToBeQueuedFirst(RemoteMovie remoteMovie)
        {
            var isRecentMovie = remoteMovie.Movie.MovieMetadata.Value.IsRecentMovie;

            if ((isRecentMovie && Settings.RecentPriority == (int)FreeboxDownloadPriority.First) ||
                (!isRecentMovie && Settings.OlderPriority == (int)FreeboxDownloadPriority.First))
            {
                return true;
            }

            return false;
        }

        private double? GetSeedRatio(RemoteMovie remoteMovie)
        {
            if (remoteMovie.SeedConfiguration == null || remoteMovie.SeedConfiguration.Ratio == null)
            {
                return null;
            }

            return remoteMovie.SeedConfiguration.Ratio.Value * 100;
        }
    }
}
