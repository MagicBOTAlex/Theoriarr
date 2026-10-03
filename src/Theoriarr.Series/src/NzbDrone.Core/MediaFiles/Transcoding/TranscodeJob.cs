using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public enum TranscodeJobStatus
    {
        Queued = 0,
        Running = 1,
        AwaitingReview = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5,
        Skipped = 6,
        Transferring = 7
    }

    public enum TranscodeMode
    {
        TargetSize = 0,
        Quality = 1,
        PercentageReduction = 2,
        Remux = 3
    }

    // The outcome of a CancelJob request: the API maps NotFound to 404, NotCancellable to 409 and
    // Cancelled to 200. Cancelled stays 0 so a default/unconfigured result is treated as a success.
    public enum CancelJobResult
    {
        Cancelled = 0,
        NotFound = 1,
        NotCancellable = 2
    }

    // The per-id outcome of a bulk resolve. A failed id is reported with its reason instead of being
    // silently dropped from the response (so the caller/SPA can say which jobs failed and why).
    public class TranscodeResolveResult
    {
        public TranscodeResolveResult(int jobId, TranscodeJob job, string error)
        {
            JobId = jobId;
            Job = job;
            Error = error;
        }

        public int JobId { get; }

        public TranscodeJob Job { get; }

        public string Error { get; }

        public bool Success => Job != null;
    }

    public class TranscodeJob : ModelBase
    {
        public MediaType MediaType { get; set; }
        public int? EpisodeFileId { get; set; }
        public int? MovieFileId { get; set; }
        public int? SeriesId { get; set; }
        public int? MovieId { get; set; }
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public string OriginalPath { get; set; }
        public TranscodeJobStatus Status { get; set; }
        public TranscodeMode Mode { get; set; }
        public string VideoCodec { get; set; }
        public string RateControl { get; set; }
        public long? TargetSize { get; set; }
        public int? TargetPercent { get; set; }
        public int? QualityValue { get; set; }
        public int? MaxHeight { get; set; }
        public string Tag { get; set; }
        public bool PreferEnglishAudio { get; set; }
        public string Preset { get; set; }
        public string DeviceId { get; set; }
        public int? ProfileId { get; set; }
        public string Container { get; set; }
        public int? RequeuedJobId { get; set; }
        public int Priority { get; set; }
        public long? SourceSize { get; set; }
        public long? OutputSize { get; set; }
        public double Progress { get; set; }
        public string Speed { get; set; }
        public string Fps { get; set; }
        public string Eta { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }
        public CommandTrigger Trigger { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }
}
