using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface IVainfoInfo
    {
        // Runs libva's `vainfo` against a DRM render node (or the default DRM device when null).
        VainfoOutput GetInfo(string renderNode);

        // Cheap identity of the installed VA-API driver, used for the capability snapshot fingerprint.
        string GetFingerprint();
    }

    public class VainfoOutput
    {
        public VainfoOutput()
        {
            Profiles = new List<string>();
            Entrypoints = new List<string>();
            EncodeProfiles = new List<string>();
        }

        public string Driver { get; set; }
        public string Version { get; set; }
        public List<string> Profiles { get; set; }
        public List<string> Entrypoints { get; set; }
        public List<string> EncodeProfiles { get; set; }
        public bool SupportsEncode => EncodeProfiles.Any();
    }

    // Thin wrapper around the libva `vainfo` binary. It maps a render node to the real VA-API driver
    // and the profiles/entrypoints it exposes, so a decode-only bridge (e.g. the NVIDIA NVDEC driver,
    // which reports only VAEntrypointVLD) is not mistaken for a hardware encoder.
    public class VainfoInfo : IVainfoInfo
    {
        private static readonly Regex DriverRegex = new Regex(@"vainfo:\s*Driver version:\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex VersionRegex = new Regex(@"vainfo:\s*VA-API version:\s*(\S+)", RegexOptions.Compiled);
        private static readonly Regex ProfileRegex = new Regex(@"^\s*(VAProfile\S+?)\s*:\s*(VAEntrypoint\S+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly string[] EncodeEntrypointNames = { "VAEntrypointEncSlice", "VAEntrypointEncSliceLP", "VAEntrypointEncPicture" };

        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public VainfoInfo(IProcessProvider processProvider, Logger logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }

        public VainfoOutput GetInfo(string renderNode)
        {
            var arguments = "--display drm";

            if (renderNode.IsNotNullOrWhiteSpace())
            {
                arguments += $" --device {renderNode}";
            }

            return Run(arguments);
        }

        public string GetFingerprint()
        {
            var info = Run("--display drm");

            return info == null ? "no-vainfo" : $"{info.Version}:{info.Driver}";
        }

        private VainfoOutput Run(string arguments)
        {
            try
            {
                // VA-API is optional and absent on CUDA-only NVIDIA hosts; capture quietly so the
                // expected probe failure is not logged as an error.
                var output = _processProvider.StartAndCapture("vainfo", arguments, null, logOutput: false);

                if (output.ExitCode != 0)
                {
                    return null;
                }

                return Parse(output.Standard.Select(line => line.Content).Join("\n"));
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "vainfo is not available");
                return null;
            }
        }

        public static VainfoOutput Parse(string output)
        {
            if (output.IsNullOrWhiteSpace())
            {
                return null;
            }

            var driver = DriverRegex.Match(output);

            if (!driver.Success)
            {
                return null;
            }

            var version = VersionRegex.Match(output);

            var result = new VainfoOutput
            {
                Driver = driver.Groups[1].Value.Trim(),
                Version = version.Success ? version.Groups[1].Value.Trim() : null
            };

            foreach (Match match in ProfileRegex.Matches(output))
            {
                var profile = match.Groups[1].Value.Trim();
                var entrypoint = match.Groups[2].Value.Trim();

                if (!result.Profiles.Contains(profile))
                {
                    result.Profiles.Add(profile);
                }

                if (!result.Entrypoints.Contains(entrypoint))
                {
                    result.Entrypoints.Add(entrypoint);
                }

                if (EncodeEntrypointNames.Contains(entrypoint) && !result.EncodeProfiles.Contains(profile))
                {
                    result.EncodeProfiles.Add(profile);
                }
            }

            return result;
        }
    }
}
