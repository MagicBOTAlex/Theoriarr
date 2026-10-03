using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface IFFmpegInfo
    {
        string GetVersion();
        List<string> GetEncoders();
        List<string> GetDecoders();
        List<string> GetFilters();
        string Run(string arguments);
        bool RunProbe(string arguments);
    }

    // Thin, cached wrapper around the resolved ffmpeg binary: capability listings and tiny probe runs.
    public class FFmpegInfo : IFFmpegInfo
    {
        private static readonly Regex CodecEntryRegex = new Regex(@"^\s*[A-Z.]{6}\s+(\S+)", RegexOptions.Compiled);

        // ffmpeg <= 7 prints a 3-char flag column for `-filters`; ffmpeg 8 shrank it to 2. Accept
        // either width. Legend rows (e.g. "T.. = Timeline support") also match and capture the "="
        // token, which ParseEntries filters out.
        private static readonly Regex FilterEntryRegex = new Regex(@"^\s*[A-Z.]{2,3}\s+(\S+)", RegexOptions.Compiled);

        private readonly IFFmpegProvider _ffmpegProvider;
        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        // Each capability list is cached together with the binary path it was read from: a single
        // shared path field let one getter stamp the new path while the other two kept serving the
        // old binary's list.
        private string _cachedEncoderPath;
        private string _cachedDecoderPath;
        private string _cachedFilterPath;
        private List<string> _encoders;
        private List<string> _decoders;
        private List<string> _filters;

        public FFmpegInfo(IFFmpegProvider ffmpegProvider, IProcessProvider processProvider, Logger logger)
        {
            _ffmpegProvider = ffmpegProvider;
            _processProvider = processProvider;
            _logger = logger;
        }

        public string GetVersion()
        {
            return _ffmpegProvider.GetVersion();
        }

        public List<string> GetEncoders()
        {
            return GetEntries(ref _encoders, ref _cachedEncoderPath, "-encoders", CodecEntryRegex);
        }

        public List<string> GetDecoders()
        {
            return GetEntries(ref _decoders, ref _cachedDecoderPath, "-decoders", CodecEntryRegex);
        }

        public List<string> GetFilters()
        {
            return GetEntries(ref _filters, ref _cachedFilterPath, "-filters", FilterEntryRegex);
        }

        public string Run(string arguments)
        {
            return Run(_ffmpegProvider.GetFFmpegPath(), arguments);
        }

        // Each list is cached per resolved ffmpeg binary; pointing the provider at a different binary
        // rebuilds it so capabilities never reflect a stale executable. A failed/empty probe (missing
        // or non-startable binary) is deliberately not cached and retried on the next call.
        private List<string> GetEntries(ref List<string> cache, ref string cachedPath, string arguments, Regex regex)
        {
            var path = _ffmpegProvider.GetFFmpegPath();

            if (cache != null && string.Equals(cachedPath, path, StringComparison.Ordinal))
            {
                return cache;
            }

            var entries = ParseEntries(Run(path, arguments), regex);

            if (entries.Count == 0)
            {
                return entries;
            }

            cache = entries;
            cachedPath = path;

            return cache;
        }

        private string Run(string path, string arguments)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var output = _processProvider.StartAndCapture(path, "-hide_banner -nostdin " + arguments);
                return output.Standard.Select(line => line.Content).Join("\n");
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to run ffmpeg {0}", arguments);
                return null;
            }
        }

        public bool RunProbe(string arguments)
        {
            var path = _ffmpegProvider.GetFFmpegPath();

            if (path.IsNullOrWhiteSpace())
            {
                return false;
            }

            try
            {
                // Probes are expected to fail on unsupported hardware; the exit code is the signal, so
                // do not mirror ffmpeg's stderr into the error log.
                var output = _processProvider.StartAndCapture(path, "-hide_banner -nostdin -loglevel error -y " + arguments, null, logOutput: false);
                return output.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "ffmpeg probe failed: {0}", arguments);
                return false;
            }
        }

        private static List<string> ParseEntries(string output, Regex regex)
        {
            if (output.IsNullOrWhiteSpace())
            {
                return new List<string>();
            }

            return output.Split('\n')
                         .Select(line => regex.Match(line))
                         .Where(match => match.Success)
                         .Select(match => match.Groups[1].Value)
                         .Where(name => name != "=")
                         .Distinct()
                         .ToList();
        }
    }
}
