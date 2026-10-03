using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // The read-only result of probing a device. Everything here is detected, never user-configured.
    public class DeviceCapability
    {
        public DeviceCapability()
        {
            Encoders = new List<string>();
            Decoders = new List<string>();
            PixelFormats = new List<string>();
            RateControls = new List<string>();
            Presets = new List<string>();
            Profiles = new List<string>();
            Entrypoints = new List<string>();
            Filters = new List<string>();
        }

        public string Id { get; set; }
        public TranscodeDeviceKind Kind { get; set; }
        public string Name { get; set; }
        public bool Supported { get; set; }
        public List<string> Encoders { get; set; }
        public List<string> Decoders { get; set; }
        public List<string> PixelFormats { get; set; }
        public List<string> RateControls { get; set; }
        public List<string> Presets { get; set; }
        public List<string> Profiles { get; set; }
        public List<string> Entrypoints { get; set; }
        public List<string> Filters { get; set; }
        public string Tonemap { get; set; }
        public int MaxSessions { get; set; }
        public long VramMB { get; set; }
        public string Driver { get; set; }
    }
}
