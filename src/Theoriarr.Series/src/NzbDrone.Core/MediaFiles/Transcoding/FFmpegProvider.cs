using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface IFFmpegProvider
    {
        string GetFFmpegPath();
        string GetVersion(string ffmpegPath = null);
    }

    // Locates the ffmpeg encoder binary. ffprobe is bundled via Openur.FFprobeStatic, but there is no
    // matching ffmpeg package, so the encoder is resolved (in order) from the configured override, a
    // binary next to the application, the process PATH and a short list of well-known install paths.
    public class FFmpegProvider : IFFmpegProvider
    {
        public const string FFmpegBinaryName = "ffmpeg";

        private static readonly Regex VersionRegex = new Regex(@"^\s*ffmpeg version\s+(\S+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly string[] CommonUnixPaths = { "/usr/bin", "/usr/local/bin", "/bin", "/opt/homebrew/bin", "/opt/local/bin", "/snap/bin" };

        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public FFmpegProvider(IConfigService configService,
                              IAppFolderInfo appFolderInfo,
                              IDiskProvider diskProvider,
                              IProcessProvider processProvider,
                              Logger logger)
        {
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _processProvider = processProvider;
            _logger = logger;
        }

        public string GetFFmpegPath()
        {
            var configured = _configService.FFmpegPath;

            if (configured.IsNotNullOrWhiteSpace())
            {
                var configuredPath = configured.Trim();

                if (_diskProvider.FileExists(configuredPath))
                {
                    return configuredPath;
                }

                _logger.Warn("Configured ffmpeg path '{0}' does not exist, falling back to auto-detection", configuredPath);
            }

            var bundledPath = Path.Combine(_appFolderInfo.StartUpFolder, BinaryName);

            if (_diskProvider.FileExists(bundledPath))
            {
                return bundledPath;
            }

            var pathPath = FindInPath();

            if (pathPath != null)
            {
                return pathPath;
            }

            if (OsInfo.IsNotWindows)
            {
                foreach (var directory in CommonUnixPaths)
                {
                    var candidate = Path.Combine(directory, BinaryName);

                    if (_diskProvider.FileExists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            _logger.Debug("Unable to locate an ffmpeg binary");

            return null;
        }

        public string GetVersion(string ffmpegPath = null)
        {
            var path = ffmpegPath ?? GetFFmpegPath();

            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var output = _processProvider.StartAndCapture(path, "-version");
                var match = VersionRegex.Match(output.Standard.Select(line => line.Content).Join("\n"));

                if (!match.Success)
                {
                    _logger.Warn("Unable to determine ffmpeg version from '{0}' (exit code {1})", path, output.ExitCode);
                    return null;
                }

                return match.Groups[1].Value;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to execute ffmpeg at '{0}'", path);
                return null;
            }
        }

        private static string BinaryName => OsInfo.IsWindows ? FFmpegBinaryName + ".exe" : FFmpegBinaryName;

        private string FindInPath()
        {
            var searchPath = Environment.GetEnvironmentVariable("PATH");

            if (searchPath.IsNullOrWhiteSpace())
            {
                return null;
            }

            foreach (var entry in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var directory = entry.Trim().Trim('"');

                if (directory.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var candidate = Path.Combine(directory, BinaryName);

                if (_diskProvider.FileExists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
