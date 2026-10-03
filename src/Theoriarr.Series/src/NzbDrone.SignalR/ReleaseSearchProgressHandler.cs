using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.SignalR
{
    // Bridges interactive-search progress events raised in the Core layer to the SignalR hubs
    // so the UI can render a loading bar per indexer while the request is still in flight.
    public class ReleaseSearchProgressHandler : IHandleAsync<ReleaseSearchProgressEvent>
    {
        private readonly IBroadcastSignalRMessage _broadcaster;

        public ReleaseSearchProgressHandler(IBroadcastSignalRMessage broadcaster)
        {
            _broadcaster = broadcaster;
        }

        public void HandleAsync(ReleaseSearchProgressEvent message)
        {
            if (!_broadcaster.IsConnected)
            {
                return;
            }

            var signalRMessage = new SignalRMessage
            {
                Name = "releaseSearchProgress",
                Version = 5,
                Subsystem = message.Domain == ReleaseSearchDomain.Movies ? SignalRSubsystem.Movies : SignalRSubsystem.Series,
                Body = new
                {
                    SearchId = message.SearchId,
                    Domain = message.Domain,
                    Status = message.Status,
                    Indexers = message.Indexers,
                    IndexerId = message.IndexerId,
                    IndexerName = message.IndexerName,
                    ReleaseCount = message.ReleaseCount,
                    Failed = message.Failed
                }
            };

            _broadcaster.BroadcastMessage(signalRMessage).GetAwaiter().GetResult();
        }
    }
}
