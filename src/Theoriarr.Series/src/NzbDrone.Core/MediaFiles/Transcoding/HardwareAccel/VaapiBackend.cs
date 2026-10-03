using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel
{
    public class VaapiBackend : IHardwareAccelBackend
    {
        public const string DriPath = "/dev/dri";

        private const string AmdVendor = "0x1002";
        private const string IntelVendor = "0x8086";
        private const string NvidiaVendor = "0x10de";

        private static readonly string[] CandidateEncoders = { "h264_vaapi", "hevc_vaapi", "av1_vaapi" };

        private readonly IFFmpegInfo _ffmpegInfo;
        private readonly IVainfoInfo _vainfoInfo;
        private readonly Logger _logger;

        public VaapiBackend(IFFmpegInfo ffmpegInfo, IVainfoInfo vainfoInfo, Logger logger)
        {
            _ffmpegInfo = ffmpegInfo;
            _vainfoInfo = vainfoInfo;
            _logger = logger;
        }

        public TranscodeDeviceKind Kind => TranscodeDeviceKind.Vaapi;

        public string GetFingerprint()
        {
            var nodes = EnumerateRenderNodes().Select(node => $"{node.Node}:{node.Vendor}").Join(",");

            return $"{_vainfoInfo.GetFingerprint()}|{nodes}";
        }

        public List<DeviceCapability> Detect()
        {
            var result = new List<DeviceCapability>();

            foreach (var node in EnumerateRenderNodes())
            {
                // Ask libva what this node actually is. A render node with no usable VA-API driver
                // (e.g. a CUDA-only NVIDIA node) reports no driver and is skipped.
                var vainfo = _vainfoInfo.GetInfo(node.Node);

                if (vainfo == null)
                {
                    continue;
                }

                var filters = _ffmpegInfo.GetFilters().Where(filter => filter.Contains("_vaapi")).ToList();

                // Only a device that advertises an encode entrypoint can be used for encoding. The
                // NVIDIA NVDEC bridge, for example, lists only VAEntrypointVLD and must not be offered
                // as an encoder even though `h264_vaapi`/`hevc_vaapi` exist in ffmpeg.
                var encoders = vainfo.SupportsEncode
                    ? CandidateEncoders.Where(encoder => _ffmpegInfo.GetEncoders().Contains(encoder) && Probe(node.Node, encoder)).ToList()
                    : new List<string>();

                result.Add(new DeviceCapability
                {
                    Id = $"vaapi:{node.Node}",
                    Kind = Kind,
                    Name = NodeName(node),
                    Supported = encoders.Any(),
                    Encoders = encoders,
                    Decoders = new List<string>(),
                    PixelFormats = new List<string> { "nv12", "p010le", "yuv420p" },
                    RateControls = new List<string> { "cqp", "vbr", "cbr" },
                    Presets = new List<string>(),
                    Profiles = vainfo.EncodeProfiles,
                    Entrypoints = vainfo.Entrypoints,
                    Filters = filters,
                    Tonemap = filters.Contains("tonemap_vaapi") ? "vaapi" : null,
                    MaxSessions = 1,
                    Driver = vainfo.Driver
                });
            }

            return result;
        }

        private bool Probe(string renderNode, string encoder)
        {
            return _ffmpegInfo.RunProbe($"-vaapi_device {renderNode} -f lavfi -i testsrc2=size=256x256:rate=1 -frames:v 1 -vf format=nv12,hwupload -c:v {encoder} -f null -");
        }

        private static string NodeName(RenderNode node)
        {
            var vendor = node.Vendor switch
            {
                AmdVendor => "AMD",
                IntelVendor => "Intel",
                NvidiaVendor => "NVIDIA",
                _ => "VA-API"
            };

            return $"{vendor} VA-API ({Path.GetFileName(node.Node)})";
        }

        private List<RenderNode> EnumerateRenderNodes()
        {
            var nodes = new List<RenderNode>();

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(DriPath).OrderBy(entry => entry))
                {
                    var name = Path.GetFileName(entry);

                    if (!name.StartsWith("renderD", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    nodes.Add(new RenderNode { Node = entry, Vendor = ReadVendor(name) });
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to enumerate VA-API render nodes in {0}", DriPath);
            }

            return nodes;
        }

        private string ReadVendor(string renderName)
        {
            try
            {
                return File.ReadAllText($"/sys/class/drm/{renderName}/device/vendor").Trim();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read vendor for {0}", renderName);
                return null;
            }
        }

        private class RenderNode
        {
            public string Node { get; set; }
            public string Vendor { get; set; }
        }
    }
}
