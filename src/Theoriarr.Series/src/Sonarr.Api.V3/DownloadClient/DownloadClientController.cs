using FluentValidation;
using NzbDrone.Core.Download;
using NzbDrone.SignalR;
using Sonarr.Http;

namespace Sonarr.Api.V3.DownloadClient
{
    // One shared download-client store serves both domains, and the unified Settings page renders
    // one merged form (Series + Movies sections) through the series key, so every field is returned
    // here too. Per-subject fields are grouped by their `section`.
    [V3ApiController]
    public class DownloadClientController : ProviderControllerBase<DownloadClientResource, DownloadClientBulkResource, IDownloadClient, DownloadClientDefinition>
    {
        public static readonly DownloadClientResourceMapper ResourceMapper = new();
        public static readonly DownloadClientBulkResourceMapper BulkResourceMapper = new();

        public DownloadClientController(IBroadcastSignalRMessage signalRBroadcaster, IDownloadClientFactory downloadClientFactory)
            : base(signalRBroadcaster, downloadClientFactory, "downloadclient", ResourceMapper, BulkResourceMapper)
        {
            SharedValidator.RuleFor(c => c.Priority).InclusiveBetween(1, 50);
        }
    }
}
