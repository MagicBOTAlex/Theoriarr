using System;
using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public class MediaCompressionCapabilities
    {
        public MediaCompressionCapabilities()
        {
            Devices = new List<TranscodeDevice>();
        }

        public string Fingerprint { get; set; }
        public DateTime? LastProbed { get; set; }
        public string FFmpegPath { get; set; }
        public string FFmpegVersion { get; set; }
        public List<TranscodeDevice> Devices { get; set; }
    }
}
