using System;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.MediaFiles.MovieImport
{
    public interface IDetectSample
    {
        DetectSampleResult IsSample(MovieMetadata movie, string path);
        DetectSampleResult IsSample(MovieMetadata movie, string path, out string reason);
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

        public DetectSampleResult IsSample(MovieMetadata movie, string path)
        {
            return IsSample(movie, path, out _);
        }

        public DetectSampleResult IsSample(MovieMetadata movie, string path, out string reason)
        {
            reason = null;

            var extension = Path.GetExtension(path);

            if (extension != null)
            {
                if (extension.Equals(".flv", StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Debug("Skipping sample check for .flv file");
                    return DetectSampleResult.NotSample;
                }

                if (extension.Equals(".strm", StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Debug("Skipping sample check for .strm file");
                    return DetectSampleResult.NotSample;
                }

                if (new string[] { ".iso", ".img", ".m2ts" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.Debug($"Skipping sample check for DVD/BR image file '{path}'");
                    return DetectSampleResult.NotSample;
                }
            }

            // TODO: Use MediaInfo from the import process, no need to re-process the file again here
            var probe = _videoFileInfoReader.ProbeMediaInfo(path);
            var runTime = probe.MediaInfo?.RunTime;

            if (!runTime.HasValue)
            {
                reason = GetIndeterminateReason(probe);
                _logger.Error("Failed to determine the runtime of '{0}': {1}", path, reason);
                return DetectSampleResult.Indeterminate;
            }

            var minimumRuntime = GetMinimumAllowedRuntime(movie);

            if (runTime.Value.TotalMinutes.Equals(0))
            {
                _logger.Error("[{0}] has a runtime of 0, is it a valid video file?", path);
                return DetectSampleResult.Sample;
            }

            if (runTime.Value.TotalSeconds < minimumRuntime)
            {
                _logger.Debug("[{0}] appears to be a sample. Runtime: {1} seconds. Expected at least: {2} seconds", path, runTime.Value.TotalSeconds, minimumRuntime);
                return DetectSampleResult.Sample;
            }

            _logger.Debug("[{0}] does not appear to be a sample. Runtime {1} seconds is more than minimum of {2} seconds", path, runTime, minimumRuntime);
            return DetectSampleResult.NotSample;
        }

        private int GetMinimumAllowedRuntime(MovieMetadata movie)
        {
            // Anime short - 15 seconds
            if (movie.Runtime <= 3)
            {
                return 15;
            }

            // Webisodes - 90 seconds
            if (movie.Runtime <= 10)
            {
                return 90;
            }

            // 30 minute episodes - 5 minutes
            if (movie.Runtime <= 30)
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
