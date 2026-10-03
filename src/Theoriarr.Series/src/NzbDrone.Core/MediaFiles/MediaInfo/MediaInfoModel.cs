using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles.MediaInfo
{
    public class MediaInfoModel : IEmbeddedDocument
    {
        public string RawStreamData { get; set; }

        // MOVIES-only flat probe payload (retained for MovieFile compatibility).
        public string RawFrameData { get; set; }

        public int SchemaRevision { get; set; }

        public string ContainerFormat { get; set; }
        public string VideoFormat { get; set; }

        public string VideoCodecID { get; set; }

        public string VideoProfile { get; set; }

        public long VideoBitrate { get; set; }

        public int VideoBitDepth { get; set; }

        public int VideoMultiViewCount { get; set; }

        // Index of the primary video stream among the file's video streams, i.e. the N in
        // "-map 0:v:N". The probe skips leading motion-image/cover-art streams, so this can be
        // non-zero; older media info deserialises to 0 (the first video stream).
        public int PrimaryVideoStreamIndex { get; set; }

        public string VideoColourPrimaries { get; set; }

        public string VideoTransferCharacteristics { get; set; }

        public HdrFormat VideoHdrFormat { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        // MOVIES-only flat audio fields (computed from AudioStreams where possible).
        public string AudioFormat { get; set; }

        public string AudioCodecID { get; set; }

        public string AudioProfile { get; set; }

        public long AudioBitrate { get; set; }

        public int AudioStreamCount { get; set; }

        public int AudioChannels { get; set; }

        public string AudioChannelPositions { get; set; }

        public TimeSpan RunTime { get; set; }

        public decimal VideoFps { get; set; }

        public List<string> AudioLanguages { get; set; }

        public List<string> Subtitles { get; set; }

        public MediaInfoAudioStreamModel PrimaryAudioStream => AudioStreams?.FirstOrDefault();

        public List<MediaInfoAudioStreamModel> AudioStreams { get; set; }

        public List<MediaInfoSubtitleStreamModel> SubtitleStreams { get; set; }

        public string ScanType { get; set; }

        [JsonIgnore]
        public string Title { get; set; }
    }
}
