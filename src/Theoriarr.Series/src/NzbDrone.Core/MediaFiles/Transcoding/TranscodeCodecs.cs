using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Single source of truth for the logical codecs a user can enable per device and the stable
    // string names shared with the API/UI/TranscodeJob.VideoCodec.
    public static class TranscodeCodecs
    {
        public static readonly IReadOnlyList<TranscodeCodec> All = new[]
        {
            TranscodeCodec.H264,
            TranscodeCodec.Hevc,
            TranscodeCodec.Av1
        };

        public static string Name(TranscodeCodec codec)
        {
            return codec switch
            {
                TranscodeCodec.H264 => "h264",
                TranscodeCodec.Av1 => "av1",
                _ => "hevc"
            };
        }

        public static TranscodeCodec Parse(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "h264" => TranscodeCodec.H264,
                "av1" => TranscodeCodec.Av1,
                _ => TranscodeCodec.Hevc
            };
        }
    }
}
