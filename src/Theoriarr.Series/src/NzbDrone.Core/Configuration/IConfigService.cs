using System.Collections.Generic;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.MetadataSource.Provider.Resource;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Security;

namespace NzbDrone.Core.Configuration
{
    public interface IConfigService
    {
        void SaveConfigDictionary(Dictionary<string, object> configValues);

        bool IsDefined(string key);

        // Download Client
        string DownloadClientWorkingFolders { get; set; }
        int DownloadClientHistoryLimit { get; set; }
        int CheckForFinishedDownloadInterval { get; set; }

        // Completed/Failed Download Handling (Download client)
        bool EnableCompletedDownloadHandling { get; set; }
        bool AutoRedownloadFailed { get; set; }
        bool AutoRedownloadFailedFromInteractiveSearch { get; set; }

        // Media Management
        bool AutoUnmonitorPreviouslyDownloadedEpisodes { get; set; }
        bool AutoUnmonitorPreviouslyDownloadedMovies { get; set; }
        string RecycleBin { get; set; }
        int RecycleBinCleanupDays { get; set; }
        string FFmpegPath { get; set; }

        // Media Compression
        string TranscodeDevicesConfig { get; set; }
        bool MediaCompressionEnabled { get; set; }
        string TranscodeTempFolder { get; set; }
        int MaxConcurrentJobs { get; set; }
        string DefaultVideoCodec { get; set; }
        TranscodeMode DefaultRateControlMode { get; set; }
        int DefaultTargetEpisodeSizeMB { get; set; }
        int DefaultTargetMovieSizeMB { get; set; }
        int DefaultQualityValue { get; set; }
        string DefaultPreset { get; set; }
        int DefaultReducePercent { get; set; }
        TranscodeReviewAction TranscodeReviewDefault { get; set; }
        bool PreferHardware { get; set; }
        bool TranscodeEasiestJobsFirst { get; set; }
        int TranscodeNice { get; set; }
        ProperDownloadTypes DownloadPropersAndRepacks { get; set; }
        bool CreateEmptySeriesFolders { get; set; }
        bool CreateEmptyMovieFolders { get; set; }
        bool DeleteEmptyFolders { get; set; }
        FileDateType FileDate { get; set; }
        bool SkipFreeSpaceCheckWhenGrabbing { get; set; }
        bool SkipFreeSpaceCheckWhenImporting { get; set; }
        int MinimumFreeSpaceWhenImporting { get; set; }
        bool CopyUsingHardlinks { get; set; }
        bool EnableMediaInfo { get; set; }
        int MediaInfoProbeMaxAttempts { get; set; }
        bool UseScriptImport { get; set; }
        string ScriptImportPath { get; set; }
        bool ImportExtraFiles { get; set; }
        string ExtraFileExtensions { get; set; }
        RescanAfterRefreshType RescanAfterRefresh { get; set; }
        ImportConflictResolution ImportConflictResolution { get; set; }
        bool AutoRenameFolders { get; set; }
        EpisodeTitleRequiredType EpisodeTitleRequired { get; set; }
        string UserRejectedExtensions { get; set; }

        // Season Pack Upgrade (Media Management)
        SeasonPackUpgradeType SeasonPackUpgrade { get; set; }
        double SeasonPackUpgradeThreshold { get; set; }

        // Permissions (Media Management)
        bool SetPermissionsLinux { get; set; }
        string ChmodFolder { get; set; }
        string ChownGroup { get; set; }

        // Indexers
        int Retention { get; set; }
        int RssSyncInterval { get; set; }
        int MaximumSize { get; set; }
        int MinimumAge { get; set; }

        bool PreferIndexerFlags { get; set; }
        int AvailabilityDelay { get; set; }
        bool AllowHardcodedSubs { get; set; }
        string WhitelistedHardcodedSubs { get; set; }

        // ListSyncLevel remains the series ListSyncLevelType enum; the movie string
        // values ("disabled"/"logOnly"/...) are mapped via CleanLibraryLevel at the API.
        ListSyncLevelType ListSyncLevel { get; set; }
        int ListSyncTag { get; set; }

        // Metadata Provider
        TMDbCountryCode CertificationCountry { get; set; }

        // Base URL of the Providarr metadata instance (empty = shared public default).
        // Applied at startup; a change requires a restart.
        string ProvidarrBaseUrl { get; set; }

        // UI
        int FirstDayOfWeek { get; set; }
        string CalendarWeekColumnHeader { get; set; }
        MovieRuntimeFormatType MovieRuntimeFormat { get; set; }

        string ShortDateFormat { get; set; }
        string LongDateFormat { get; set; }
        string TimeFormat { get; set; }
        string TimeZone { get; set; }
        bool ShowRelativeDates { get; set; }
        bool EnableColorImpairedMode { get; set; }
        int MovieInfoLanguage { get; set; }
        int UILanguage { get; set; }

        // Internal
        bool CleanupMetadataImages { get; set; }
        string PlexClientIdentifier { get; }

        // Forms Auth
        string RijndaelPassphrase { get; }
        string HmacPassphrase { get; }
        string RijndaelSalt { get; }
        string HmacSalt { get; }

        // Proxy
        bool ProxyEnabled { get; }
        ProxyType ProxyType { get; }
        string ProxyHostname { get; }
        int ProxyPort { get; }
        string ProxyUsername { get; }
        string ProxyPassword { get; }
        string ProxyBypassFilter { get; }
        bool ProxyBypassLocalAddresses { get; }

        // Backups
        string BackupFolder { get; }
        int BackupInterval { get; }
        int BackupRetention { get; }

        CertificateValidationType CertificateValidation { get; }
        string ApplicationUrl { get; }
    }
}
