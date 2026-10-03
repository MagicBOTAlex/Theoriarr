using FluentValidation;
using NzbDrone.Core.Download;
using NzbDrone.SignalR;
using Sonarr.Api.V5.Provider;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.DownloadClient;

// One shared download-client store serves both domains, and the unified Settings page renders one
// merged form (Series + Movies sections) through the series key, so every field is returned here.
// Per-subject fields are grouped by their `section`; the movie category itself is hidden because
// the shared "theoriarr" category is used for both.
[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
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
