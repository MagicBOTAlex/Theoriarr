using System.Collections.Generic;

namespace Sonarr.Api.V3.MediaCompression
{
    public class TranscodeJobRequestResource
    {
        public List<int> EpisodeFileIds { get; set; }
        public List<int> MovieFileIds { get; set; }
        public List<int> SeriesIds { get; set; }
        public List<int> SeasonNumbers { get; set; }
        public List<int> MovieIds { get; set; }
        public string Codec { get; set; }
        public string Mode { get; set; }
        public long? TargetSize { get; set; }
        public int? TargetPercent { get; set; }
        public int? Quality { get; set; }
        public int? MaxHeight { get; set; }
        public string Preset { get; set; }
        public string DeviceId { get; set; }
        public int? ProfileId { get; set; }
        public string Container { get; set; }
        public int Priority { get; set; }
        public bool Force { get; set; }

        // History job ids being retried. When set, their files are what gets queued (the file/series
        // id lists are ignored) and each source job is stamped with the job it was requeued into, so
        // it cannot be requeued twice from the UI.
        public List<int> RequeueJobIds { get; set; }
    }

    public class TranscodeJobsResource
    {
        public List<TranscodeJobResource> Jobs { get; set; }
        public long ProjectedSavingsBytes { get; set; }

        // Number of distinct files the request resolved/targeted (after series/movie expansion) and
        // how many of them were not queued (already active, duplicate path, vanished, no path).
        public int RequestedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ResolveTranscodeJobResource
    {
        public string Action { get; set; }
    }

    public class ForceStopTranscodeJobsResource
    {
        public List<int> JobIds { get; set; }
    }

    public class BulkResolveTranscodeJobsResource
    {
        public string Action { get; set; }
        public List<int> JobIds { get; set; }
    }
}
