using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel
{
    public class NvencBackend : IHardwareAccelBackend
    {
        // NVENC requires a frame size of at least 145x49; 256x256 is a safe, cheap probe.
        public const string ProbeInput = "-f lavfi -i testsrc2=size=256x256:rate=1 -frames:v 1";

        private static readonly string[] CandidateEncoders = { "h264_nvenc", "hevc_nvenc", "av1_nvenc" };
        private static readonly string[] CandidateDecoders = { "h264_cuvid", "hevc_cuvid", "av1_cuvid" };
        private static readonly string[] NvencPresets = { "p1", "p2", "p3", "p4", "p5", "p6", "p7" };

        private readonly IFFmpegInfo _ffmpegInfo;
        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public NvencBackend(IFFmpegInfo ffmpegInfo, IProcessProvider processProvider, Logger logger)
        {
            _ffmpegInfo = ffmpegInfo;
            _processProvider = processProvider;
            _logger = logger;
        }

        public TranscodeDeviceKind Kind => TranscodeDeviceKind.Nvidia;

        public string GetFingerprint()
        {
            return QueryNvidiaSmi() ?? "no-nvidia";
        }

        public List<DeviceCapability> Detect()
        {
            var result = new List<DeviceCapability>();
            var nvidiaSmi = QueryNvidiaSmi();

            if (nvidiaSmi.IsNullOrWhiteSpace())
            {
                return result;
            }

            foreach (var gpu in ParseGpus(nvidiaSmi))
            {
                var encoders = CandidateEncoders
                    .Where(encoder => _ffmpegInfo.GetEncoders().Contains(encoder) && Probe(encoder, gpu.Index))
                    .ToList();

                var decoders = CandidateDecoders.Where(decoder => _ffmpegInfo.GetDecoders().Contains(decoder)).ToList();

                // `tonemap_opencl` has no `_cuda` suffix, so check the full filter list for it rather
                // than the CUDA-filtered subset (which could never contain it).
                var allFilters = _ffmpegInfo.GetFilters();
                var filters = allFilters.Where(filter => filter.Contains("_cuda")).ToList();

                result.Add(new DeviceCapability
                {
                    Id = $"nvenc:{gpu.Index}",
                    Kind = Kind,
                    Name = $"{gpu.Name} (GPU {gpu.Index})",
                    Supported = encoders.Any(),
                    Encoders = encoders,
                    Decoders = decoders,
                    PixelFormats = new List<string> { "yuv420p", "nv12", "p010le", "yuv444p" },
                    RateControls = new List<string> { "vbr", "cbr", "constqp" },
                    Presets = NvencPresets.ToList(),
                    Profiles = new List<string> { "main", "main10", "high444p" },
                    Filters = filters,
                    Tonemap = allFilters.Contains("tonemap_opencl") ? "opencl" : null,
                    MaxSessions = 8,
                    VramMB = gpu.VramMB,
                    Driver = gpu.Driver
                });
            }

            return result;
        }

        private bool Probe(string encoder, int gpuIndex)
        {
            return _ffmpegInfo.RunProbe($"{ProbeInput} -c:v {encoder} -gpu {gpuIndex} -f null -");
        }

        private string QueryNvidiaSmi()
        {
            try
            {
                // nvidia-smi is absent on non-NVIDIA hosts; capture quietly so the expected probe
                // failure is not logged as an error.
                var output = _processProvider.StartAndCapture("nvidia-smi", "--query-gpu=index,name,memory.total,driver_version --format=csv,noheader,nounits", null, logOutput: false);

                if (output.ExitCode != 0)
                {
                    return null;
                }

                return output.Standard.Select(line => line.Content).Join("\n");
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "nvidia-smi is not available");
                return null;
            }
        }

        private List<NvidiaGpu> ParseGpus(string nvidiaSmi)
        {
            var gpus = new List<NvidiaGpu>();

            foreach (var line in nvidiaSmi.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split(',').Select(field => field.Trim()).ToArray();

                if (fields.Length < 4 || !int.TryParse(fields[0], out var index))
                {
                    continue;
                }

                gpus.Add(new NvidiaGpu
                {
                    Index = index,
                    Name = fields[1],
                    VramMB = long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vram) ? vram : 0,
                    Driver = fields[3]
                });
            }

            return gpus;
        }

        private class NvidiaGpu
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public long VramMB { get; set; }
            public string Driver { get; set; }
        }
    }
}
