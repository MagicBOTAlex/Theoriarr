using System;
using System.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Shared validation/normalisation for the free-text transcode inputs that end up verbatim in
    // the ffmpeg argv: the remux container whitelist and the encoder preset. The one-off job
    // request path rejects invalid values (HTTP 400); profile persistence drops them to "unset"
    // (matching how an unknown codec/container is coerced).
    public static class TranscodeInput
    {
        public const int MaxPresetLength = 100;

        public static readonly string[] SupportedContainers = { "mkv", "mp4", "mov" };

        public static bool IsSupportedContainer(string container)
        {
            return container.IsNotNullOrWhiteSpace() &&
                   SupportedContainers.Contains(container.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        // Returns the canonical (trimmed, lower-cased) container for a supported value, or the
        // default when it is blank/unsupported.
        public static string NormalizeContainer(string container)
        {
            return IsSupportedContainer(container) ? container.Trim().ToLowerInvariant() : SupportedContainers[0];
        }

        // A null/blank preset is valid and means "unset". A non-blank preset must be a single
        // token: overlong, control-character or embedded-whitespace values are rejected (the
        // latter would let a preset inject extra ffmpeg arguments).
        public static bool IsValidPreset(string preset)
        {
            var trimmed = preset?.Trim();

            if (trimmed.IsNullOrWhiteSpace())
            {
                return true;
            }

            return trimmed.Length <= MaxPresetLength &&
                   !trimmed.Any(character => char.IsControl(character) || char.IsWhiteSpace(character));
        }

        public static string NormalizePreset(string preset)
        {
            var trimmed = preset?.Trim();

            if (trimmed.IsNullOrWhiteSpace() || !IsValidPreset(trimmed))
            {
                return null;
            }

            return trimmed;
        }

        // A scale target must be an even, positive height: ffmpeg's scale filter rejects an odd
        // output height (`h=1` on many encoders) and a height below 2 is meaningless. Odd values are
        // rounded up to the next even number and anything below 2 is floored to 2; non-positive/
        // missing values stay null (no cap).
        public static int? NormalizeMaxHeight(int? maxHeight)
        {
            if (maxHeight is not > 0)
            {
                return null;
            }

            var value = Math.Max(2, maxHeight.Value);

            return value % 2 == 0 ? value : value + 1;
        }

        // Free-form per-device ffmpeg arguments are administrator-supplied (the device config is
        // admin-only), but they are interpolated verbatim into the single argv string, so a quote or
        // a newline could re-balance the surrounding quoting and smuggle extra options. Reject those
        // characters rather than trying to re-quote an opaque option fragment.
        public static bool IsValidExtraArgs(string extraArgs)
        {
            if (extraArgs.IsNullOrWhiteSpace())
            {
                return true;
            }

            return !extraArgs.Any(character => character == '"' || character == '\'' ||
                                               character == '\r' || character == '\n' ||
                                               char.IsControl(character));
        }
    }
}
