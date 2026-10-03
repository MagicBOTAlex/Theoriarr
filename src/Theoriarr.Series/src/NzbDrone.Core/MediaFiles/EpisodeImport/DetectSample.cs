using System;
using System.IO;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport
{
    public interface IDetectSample
    {
        DetectSampleResult IsSample(Series series, string path, bool isSpecial);
        DetectSampleResult IsSample(Series series, string path, bool isSpecial, out string reason);
        DetectSampleResult IsSample(LocalEpisode localEpisode);
        DetectSampleResult IsSample(LocalEpisode localEpisode, out string reason);
    }

    public class DetectSample : IDetectSample
    {
        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly Logger _logger;

        public DetectSample(IVideoFileInfoReader videoFileInfoReader, Logger logger)
        {
            _videoFileInfoReader = videoFileInfoReader;
            _logger = logger;
        }

        public DetectSampleResult IsSample(Series series, string path, bool isSpecial)
        {
            return IsSample(series, path, isSpecial, out _);
        }

        public DetectSampleResult IsSample(Series series, string path, bool isSpecial, out string reason)
        {
            reason = null;

            var extensionResult = IsSample(path, isSpecial);

            if (extensionResult != DetectSampleResult.Indeterminate)
            {
                return extensionResult;
            }

            var probe = _videoFileInfoReader.ProbeMediaInfo(path);
            var fileRuntime = probe.MediaInfo?.RunTime;

            if (!fileRuntime.HasValue)
            {
                reason = GetIndeterminateReason(probe);
                _logger.Error("Failed to determine the runtime of '{0}': {1}", path, reason);
                return DetectSampleResult.Indeterminate;
            }

            return IsSample(path, fileRuntime.Value, series.Runtime);
        }

        public DetectSampleResult IsSample(LocalEpisode localEpisode)
        {
            return IsSample(localEpisode, out _);
        }

        public DetectSampleResult IsSample(LocalEpisode localEpisode, out string reason)
        {
            reason = null;

            var extensionResult = IsSample(localEpisode.Path, localEpisode.IsSpecial);

            if (extensionResult != DetectSampleResult.Indeterminate)
            {
                return extensionResult;
            }

            var runtime = 0;

            foreach (var episode in localEpisode.Episodes)
            {
                runtime += episode.Runtime > 0 ? episode.Runtime : localEpisode.Series.Runtime;
            }

            if (localEpisode.MediaInfo == null)
            {
                reason = GetIndeterminateReason(_videoFileInfoReader.ProbeMediaInfo(localEpisode.Path));
                _logger.Error("Failed to determine the runtime of '{0}': {1}", localEpisode.Path, reason);
                return DetectSampleResult.Indeterminate;
            }

            if (runtime == 0)
            {
                _logger.Debug("Series runtime is 0, defaulting runtime to 45 minutes");
                runtime = 45;
            }

            return IsSample(localEpisode.Path, localEpisode.MediaInfo.RunTime, runtime);
        }

        private DetectSampleResult IsSample(string path, bool isSpecial)
        {
            if (isSpecial)
            {
                _logger.Debug("Special, skipping sample check");
                return DetectSampleResult.NotSample;
            }

            var extension = Path.GetExtension(path);

            if (extension != null && extension.Equals(".flv", StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.Debug("Skipping sample check for .flv file");
                return DetectSampleResult.NotSample;
            }

            if (extension != null && extension.Equals(".strm", StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.Debug("Skipping sample check for .strm file");
                return DetectSampleResult.NotSample;
            }

            return DetectSampleResult.Indeterminate;
        }

        private DetectSampleResult IsSample(string path, TimeSpan fileRuntime, int expectedRuntime)
        {
            var minimumRuntime = GetMinimumAllowedRuntime(expectedRuntime);

            if (fileRuntime.TotalMinutes.Equals(0))
            {
                _logger.Error("[{0}] has a runtime of 0, is it a valid video file?", path);
                return DetectSampleResult.Sample;
            }

            if (fileRuntime.TotalSeconds < minimumRuntime)
            {
                _logger.Debug("[{0}] appears to be a sample. Runtime: {1} seconds. Expected at least: {2} seconds", path, fileRuntime, minimumRuntime);
                return DetectSampleResult.Sample;
            }

            _logger.Debug("[{0}] does not appear to be a sample. Runtime {1} seconds is more than minimum of {2} seconds", path, fileRuntime, minimumRuntime);
            return DetectSampleResult.NotSample;
        }

        private int GetMinimumAllowedRuntime(int runtime)
        {
            // Anime short - 15 seconds
            if (runtime <= 3)
            {
                return 15;
            }

            // Webisodes - 90 seconds
            if (runtime <= 10)
            {
                return 90;
            }

            // 30 minute episodes - 5 minutes
            if (runtime <= 30)
            {
                return 300;
            }

            // 60 minute episodes - 10 minutes
            return 600;
        }

        private static string GetIndeterminateReason(MediaInfoProbeResult probe)
        {
            if (probe.Error.IsNotNullOrWhiteSpace())
            {
                return $"the file could not be read ({probe.Error})";
            }

            return "the file could not be read, make sure ffprobe is available";
        }
    }
}
