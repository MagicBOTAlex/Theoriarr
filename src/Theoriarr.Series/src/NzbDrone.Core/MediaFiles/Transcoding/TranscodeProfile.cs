using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // A reusable, user-named transcode preset. A profile captures the codec/mode/quality (or a
    // stream-copy Remux) plus an optional preferred device, so a transcode can be started with one
    // pick instead of configuring every field.
    public class TranscodeProfile : ModelBase
    {
        public string Name { get; set; }
        public string Codec { get; set; }
        public TranscodeMode Mode { get; set; }
        public int QualityValue { get; set; }
        public string Preset { get; set; }
        public int? TargetSizeMB { get; set; }
        public int? TargetPercent { get; set; }

        // Cap the output height (e.g. 1080/1440); null keeps the source resolution. Sources at or
        // below the cap are left untouched (the builder never upscales).
        public int? MaxHeight { get; set; }

        // Optional filename tag marking the output as no longer raw (e.g. "Transcoded" →
        // "Movie [Transcoded].mkv"). Null/empty leaves the name untouched.
        public string Tag { get; set; }

        // Put an English audio track first (kept, not dropped) when the source has one.
        public bool PreferEnglishAudio { get; set; }
        public string DeviceId { get; set; }

        // Only meaningful for Remux: the container the streams are copied into (mkv/mp4).
        public string Container { get; set; }
        public bool IsDefault { get; set; }
    }
}
