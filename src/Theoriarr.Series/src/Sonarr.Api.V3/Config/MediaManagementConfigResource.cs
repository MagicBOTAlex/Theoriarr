using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Qualities;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class MediaManagementConfigResource : RestResource
    {
        public bool AutoUnmonitorPreviouslyDownloadedEpisodes { get; set; }
        public string RecycleBin { get; set; }
        public int RecycleBinCleanupDays { get; set; }
        public ProperDownloadTypes DownloadPropersAndRepacks { get; set; }
        public bool CreateEmptySeriesFolders { get; set; }
        public bool DeleteEmptyFolders { get; set; }
        public FileDateType FileDate { get; set; }
        public RescanAfterRefreshType RescanAfterRefresh { get; set; }
        public ImportConflictResolution ImportConflictResolution { get; set; }

        public bool SetPermissionsLinux { get; set; }
        public string ChmodFolder { get; set; }
        public string ChownGroup { get; set; }

        public EpisodeTitleRequiredType EpisodeTitleRequired { get; set; }
        public bool SkipFreeSpaceCheckWhenGrabbing { get; set; }
        public bool SkipFreeSpaceCheckWhenImporting { get; set; }
        public int MinimumFreeSpaceWhenImporting { get; set; }
        public bool CopyUsingHardlinks { get; set; }
        public bool UseScriptImport { get; set; }
        public string ScriptImportPath { get; set; }
        public bool ImportExtraFiles { get; set; }
        public string ExtraFileExtensions { get; set; }
        public bool EnableMediaInfo { get; set; }
        public int MediaInfoProbeMaxAttempts { get; set; }
        public string UserRejectedExtensions { get; set; }
        public SeasonPackUpgradeType SeasonPackUpgrade { get; set; }
        public double SeasonPackUpgradeThreshold { get; set; }

        // Movies (MOVIES union)
        public bool AutoUnmonitorPreviouslyDownloadedMovies { get; set; }
        public bool AutoRenameFolders { get; set; }
        public bool PathsDefaultStatic { get; set; }
        public bool CreateEmptyMovieFolders { get; set; }
    }

    public static class MediaManagementConfigResourceMapper
    {
        public static MediaManagementConfigResource ToResource(IConfigService model)
        {
            return new MediaManagementConfigResource
            {
                AutoUnmonitorPreviouslyDownloadedEpisodes = model.AutoUnmonitorPreviouslyDownloadedEpisodes,
                RecycleBin = model.RecycleBin,
                RecycleBinCleanupDays = model.RecycleBinCleanupDays,
                DownloadPropersAndRepacks = model.DownloadPropersAndRepacks,
                CreateEmptySeriesFolders = model.CreateEmptySeriesFolders,
                DeleteEmptyFolders = model.DeleteEmptyFolders,
                FileDate = model.FileDate,
                RescanAfterRefresh = model.RescanAfterRefresh,
                ImportConflictResolution = model.ImportConflictResolution,

                SetPermissionsLinux = model.SetPermissionsLinux,
                ChmodFolder = model.ChmodFolder,
                ChownGroup = model.ChownGroup,

                EpisodeTitleRequired = model.EpisodeTitleRequired,
                SkipFreeSpaceCheckWhenImporting = model.SkipFreeSpaceCheckWhenImporting,
                SkipFreeSpaceCheckWhenGrabbing = model.SkipFreeSpaceCheckWhenGrabbing,
                MinimumFreeSpaceWhenImporting = model.MinimumFreeSpaceWhenImporting,
                CopyUsingHardlinks = model.CopyUsingHardlinks,
                UseScriptImport = model.UseScriptImport,
                ScriptImportPath = model.ScriptImportPath,
                ImportExtraFiles = model.ImportExtraFiles,
                ExtraFileExtensions = model.ExtraFileExtensions,
                EnableMediaInfo = model.EnableMediaInfo,
                MediaInfoProbeMaxAttempts = model.MediaInfoProbeMaxAttempts,
                UserRejectedExtensions = model.UserRejectedExtensions,
                SeasonPackUpgrade = model.SeasonPackUpgrade,
                SeasonPackUpgradeThreshold = model.SeasonPackUpgradeThreshold,
                AutoUnmonitorPreviouslyDownloadedMovies = model.AutoUnmonitorPreviouslyDownloadedMovies,
                AutoRenameFolders = model.AutoRenameFolders,
                CreateEmptyMovieFolders = model.CreateEmptyMovieFolders
            };
        }
    }
}
