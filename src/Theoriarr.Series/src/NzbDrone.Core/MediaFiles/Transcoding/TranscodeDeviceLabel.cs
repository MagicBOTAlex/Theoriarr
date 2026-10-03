using System;
using System.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Builds the short, human-readable label shown next to a transcode job (e.g. "GTX 1080:0"
    // instead of "nvenc:0"). Each device kind gets its own formatting so new hardware families
    // (AMD/Intel, etc.) can plug in by adding a case here.
    public static class TranscodeDeviceLabel
    {
        public static string Build(TranscodeDevice device)
        {
            if (device == null)
            {
                return null;
            }

            return device.Kind switch
            {
                TranscodeDeviceKind.Nvidia => BuildNvidia(device),
                _ => device.Name
            };
        }

        private static string BuildNvidia(TranscodeDevice device)
        {
            var name = device.Name ?? string.Empty;

            // Drop the " (GPU N)" suffix added by the detector.
            var suffixIndex = name.IndexOf(" (GPU ", StringComparison.OrdinalIgnoreCase);

            if (suffixIndex >= 0)
            {
                name = name.Substring(0, suffixIndex);
            }

            name = name.Replace("NVIDIA ", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Replace("GeForce ", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Trim();

            if (name.IsNullOrWhiteSpace())
            {
                return device.Name;
            }

            var index = device.Id?.Split(':').LastOrDefault();

            return index.IsNullOrWhiteSpace() ? name : $"{name}:{index}";
        }
    }
}
