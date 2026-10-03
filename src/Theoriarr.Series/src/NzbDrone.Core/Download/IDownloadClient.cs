using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Download
{
    public interface IDownloadClient : IProvider
    {
        DownloadProtocol Protocol { get; }
        Task<string> Download(IRemoteSubject subject, IIndexer indexer);
        IEnumerable<DownloadClientItem> GetItems();
        DownloadClientItem GetImportItem(DownloadClientItem item, DownloadClientItem previousImportAttempt);
        void RemoveItem(DownloadClientItem item, bool deleteData);
        DownloadClientInfo GetStatus();
        void MarkItemAsImported(DownloadClientItem downloadClientItem);

        // Whether this client can be told to move a download's content to a different location.
        // Only qBittorrent's Web API exposes a standard way to do this; used by the queue's
        // "fix path" action.
        bool SupportsPathCorrection { get; }
        void SetDownloadLocation(DownloadClientItem item, string localPath);
    }
}
