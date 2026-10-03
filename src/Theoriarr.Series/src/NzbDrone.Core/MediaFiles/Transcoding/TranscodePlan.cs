using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // A single, fully resolved intent for one encode: which job, which device, which codec/mode and
    // the concrete ffmpeg invocations (software target-size uses two passes).
    public class TranscodePlan
    {
        public TranscodePlan()
        {
            Commands = new List<string>();
        }

        public TranscodeJob Job { get; set; }
        public TranscodeDevice Device { get; set; }
        public TranscodeCodec Codec { get; set; }
        public string Encoder { get; set; }
        public TranscodeMode Mode { get; set; }
        public int QualityValue { get; set; }
        public long VideoBitrateBps { get; set; }
        public string Preset { get; set; }

        // When set, a per-backend scale filter caps the output height (never upscales).
        public string ScaleFilter { get; set; }

        // Stream mapping for this job (may put an English audio track first).
        public string MapArguments { get; set; }
        public double DurationSeconds { get; set; }
        public string OutputPath { get; set; }

        // ffmpeg argument strings, executed in order. Only the last command carries progress.
        public List<string> Commands { get; set; }
    }
}
