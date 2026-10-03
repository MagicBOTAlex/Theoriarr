using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.SignalR
{
    // Bridges transcode job progress raised in the Core layer to the SignalR hubs.
    public class TranscodeProgressHandler : IHandleAsync<TranscodeProgressEvent>
    {
        private readonly IBroadcastSignalRMessage _broadcaster;

        public TranscodeProgressHandler(IBroadcastSignalRMessage broadcaster)
        {
            _broadcaster = broadcaster;
        }

        public void HandleAsync(TranscodeProgressEvent message)
        {
            if (!_broadcaster.IsConnected)
            {
                return;
            }

            var signalRMessage = new SignalRMessage
            {
                Name = "transcodeProgress",
                Version = 5,
                Subsystem = message.MediaType == MediaType.Movie ? SignalRSubsystem.Movies : SignalRSubsystem.Series,
                Body = new
                {
                    message.JobId,
                    Status = message.Status.ToString(),
                    message.Progress,
                    message.Speed,
                    message.Fps,
                    message.Eta,
                    message.DeviceId,
                    message.SizeBefore,
                    message.SizeAfter,
                    message.Message
                }
            };

            _broadcaster.BroadcastMessage(signalRMessage).GetAwaiter().GetResult();
        }
    }
}
