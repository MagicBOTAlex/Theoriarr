using System;
using System.Diagnostics;
using NzbDrone.Common.Disk;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Download
{
    [DebuggerDisplay("{DownloadClientInfo?.Name}:{Title}")]
    public class DownloadClientItem
    {
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }

        // The media type this download was resolved to by the tracked-download layer (from grab
        // history, or title parsing for manually added downloads). Never inferred from the
        // category: with the unified "theoriarr" category that would misclassify everything as a
        // movie. Null means "not resolved" (clients then fall back to the series behaviour).
        public MediaType? MediaType { get; set; }
        public long TotalSize { get; set; }
        public long RemainingSize { get; set; }
        public TimeSpan? RemainingTime { get; set; }
        public double? SeedRatio { get; set; }
        public OsPath OutputPath { get; set; }
        public string Message { get; set; }
        public DownloadItemStatus Status { get; set; }
        public bool IsEncrypted { get; set; }
        public bool CanMoveFiles { get; set; }
        public bool CanBeRemoved { get; set; }

        // The path the download's files are actually at, when the download client's reported path
        // is wrong/unreachable and the client could work out the right one (currently qBittorrent).
        // Null when no correction was needed or possible. The queue uses it to offer a "Fix path"
        // action; nothing is moved automatically.
        public string SuggestedOutputPath { get; set; }

        // True when the path Theoriarr would import from does not exist on disk. Set by the client
        // when it resolves the import item, so the queue can explain the path problem to the user.
        public bool PathNotAccessible { get; set; }

        public DownloadClientItem Clone()
        {
            return MemberwiseClone() as DownloadClientItem;
        }
    }

    public class DownloadClientItemClientInfo
    {
        public DownloadProtocol Protocol { get; set; }
        public string Type { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public bool RemoveCompletedDownloads { get; set; }
        public bool HasPostImportCategory { get; set; }
        public bool SupportsPathCorrection { get; set; }

        public static DownloadClientItemClientInfo FromDownloadClient<TSettings>(
            DownloadClientBase<TSettings> downloadClient, bool hasPostImportCategory)
            where TSettings : IProviderConfig, new()
        {
            return new DownloadClientItemClientInfo
            {
                Protocol = downloadClient.Protocol,
                Type = downloadClient.Name,
                Id = downloadClient.Definition.Id,
                Name = downloadClient.Definition.Name,
                RemoveCompletedDownloads = downloadClient.Definition is DownloadClientDefinition { RemoveCompletedDownloads: true },
                HasPostImportCategory = hasPostImportCategory,
                SupportsPathCorrection = downloadClient.SupportsPathCorrection
            };
        }
    }
}
