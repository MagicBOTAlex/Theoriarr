using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel
{
    public class SoftwareBackend : IHardwareAccelBackend
    {
        public const string DeviceId = "software";

        private static readonly string[] CandidateEncoders = { "libx264", "libx265", "libsvtav1", "libaom-av1", "libvpx-vp9" };
        private static readonly string[] SoftwarePresets = { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" };

        private readonly IFFmpegInfo _ffmpegInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SoftwareBackend(IFFmpegInfo ffmpegInfo, IDiskProvider diskProvider, Logger logger)
        {
            _ffmpegInfo = ffmpegInfo;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public TranscodeDeviceKind Kind => TranscodeDeviceKind.Software;

        public string GetFingerprint()
        {
            return GetCpuName() ?? "unknown-cpu";
        }

        public List<DeviceCapability> Detect()
        {
            var encoders = CandidateEncoders.Where(encoder => _ffmpegInfo.GetEncoders().Contains(encoder)).ToList();
            var cpuName = GetCpuName();

            return new List<DeviceCapability>
            {
                new DeviceCapability
                {
                    Id = DeviceId,
                    Kind = Kind,
                    Name = cpuName.IsNullOrWhiteSpace() ? "CPU" : $"CPU ({cpuName})",
                    Supported = encoders.Any(),
                    Encoders = encoders,
                    Decoders = new List<string>(),
                    PixelFormats = new List<string> { "yuv420p", "yuv420p10le", "yuv444p" },
                    RateControls = new List<string> { "crf", "vbr" },
                    Presets = SoftwarePresets.ToList(),
                    Profiles = new List<string> { "main", "main10", "high" },
                    Filters = new List<string> { "scale", "yadif" },
                    Tonemap = "software",
                    MaxSessions = 1
                }
            };
        }

        private string GetCpuName()
        {
            try
            {
                var cpuInfo = _diskProvider.ReadAllText("/proc/cpuinfo");
                var modelLine = cpuInfo?.Split('\n').FirstOrDefault(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase));

                if (modelLine == null)
                {
                    return null;
                }

                var separator = modelLine.IndexOf(':');

                return separator >= 0 ? modelLine[(separator + 1)..].Trim() : modelLine.Trim();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read CPU information");
                return null;
            }
        }
    }
}
