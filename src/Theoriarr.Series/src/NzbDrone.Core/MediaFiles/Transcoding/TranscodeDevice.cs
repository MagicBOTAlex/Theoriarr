using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public enum TranscodeDeviceKind
    {
        Software = 0,
        Nvidia = 1,
        Vaapi = 2,
        Qsv = 3,
        Amf = 4
    }

    // A detected device merged with the user's control settings. Persisted as JSON under the
    // "TranscodeDevicesConfig" config key, matched across probes by the stable Id.
    public class TranscodeDevice
    {
        public string Id { get; set; }
        public TranscodeDeviceKind Kind { get; set; }
        public string Name { get; set; }
        public bool Supported { get; set; }
        public bool Enabled { get; set; }
        public int MaxParallel { get; set; }
        public int Priority { get; set; }
        public int Weight { get; set; }
        public bool Unavailable { get; set; }
        public TranscodeDeviceOptions Options { get; set; }
        public List<TranscodeCodecSetting> Codecs { get; set; } = new List<TranscodeCodecSetting>();
        public DeviceCapability Capabilities { get; set; }
    }

    // One logical codec on one device: the detected encoder (null when the probe found none), whether
    // the device supports it, and the user's enable/disable choice. Unsupported codecs default to off
    // but can be forced on (the encoder is then resolved by convention and the encode may fail).
    public class TranscodeCodecSetting
    {
        public string Codec { get; set; }
        public string Encoder { get; set; }
        public bool Supported { get; set; }
        public bool Enabled { get; set; }
    }

    public class TranscodeDeviceOptions
    {
        public TranscodeDeviceOptions()
        {
            Cpu = new CpuTranscodeOptions();
            Nvidia = new NvidiaTranscodeOptions();
            Vaapi = new VaapiTranscodeOptions();
        }

        public CpuTranscodeOptions Cpu { get; set; }
        public NvidiaTranscodeOptions Nvidia { get; set; }
        public VaapiTranscodeOptions Vaapi { get; set; }
    }

    public class CpuTranscodeOptions
    {
        public int Threads { get; set; }

        // 0 means "inherit the global TranscodeNice"; only an explicit non-zero value overrides it.
        public int Nice { get; set; }
    }

    public class NvidiaTranscodeOptions
    {
        public bool DecodeAccel { get; set; } = true;
        public string ExtraArgs { get; set; } = string.Empty;
    }

    public class VaapiTranscodeOptions
    {
        public bool DecodeAccel { get; set; } = true;
        public string ExtraArgs { get; set; } = string.Empty;
    }
}
