using NzbDrone.Common.Messaging;
using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Published whenever a job changes state or reports progress so the SPA can update live.
    public class TranscodeProgressEvent : IEvent
    {
        public int JobId { get; set; }
        public MediaType MediaType { get; set; }
        public TranscodeJobStatus Status { get; set; }
        public double Progress { get; set; }
        public string Speed { get; set; }
        public string Fps { get; set; }
        public string Eta { get; set; }
        public string DeviceId { get; set; }
        public long? SizeBefore { get; set; }
        public long? SizeAfter { get; set; }
        public string Message { get; set; }
    }
}
