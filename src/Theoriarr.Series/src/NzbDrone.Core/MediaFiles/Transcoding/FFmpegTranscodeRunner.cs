using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public class FFmpegTranscodeRunner : ITranscodeRunner
    {
        private const int ErrorTailLength = 25;

        // How often the wait loop wakes to re-check the liveness deadlines.
        private const int WaitPollMilliseconds = 1000;

        private const double DurationTimeoutFactor = 12;

        // Liveness is progress-based, not wall-clock: a slow software encode (x265 slow, AV1, 4K) may
        // legitimately run for hours, so it is only killed once it stops emitting output for this
        // bounded window. The absolute ceiling is a backstop against an encode that keeps making
        // progress forever (e.g. an infinite source) and is intentionally large.
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MinimumCommandTimeout = TimeSpan.FromHours(2);
        private static readonly TimeSpan MaximumCommandTimeout = TimeSpan.FromHours(24);

        private readonly IFFmpegProvider _ffmpegProvider;
        private readonly IDiskProvider _diskProvider;
        private readonly IProcessProvider _processProvider;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public FFmpegTranscodeRunner(IFFmpegProvider ffmpegProvider,
                                     IDiskProvider diskProvider,
                                     IProcessProvider processProvider,
                                     IConfigService configService,
                                     Logger logger)
        {
            _ffmpegProvider = ffmpegProvider;
            _diskProvider = diskProvider;
            _processProvider = processProvider;
            _configService = configService;
            _logger = logger;
        }

        public TranscodeResult Run(TranscodePlan plan, Action<TranscodeProgress> onProgress, CancellationToken cancellationToken)
        {
            var ffmpeg = _ffmpegProvider.GetFFmpegPath();

            if (ffmpeg.IsNullOrWhiteSpace())
            {
                return Failure("ffmpeg was not found");
            }

            var outputFolder = Path.GetDirectoryName(plan.OutputPath);

            if (outputFolder.IsNotNullOrWhiteSpace() && !_diskProvider.FolderExists(outputFolder))
            {
                _diskProvider.CreateFolder(outputFolder);
            }

            for (var index = 0; index < plan.Commands.Count; index++)
            {
                var isLast = index == plan.Commands.Count - 1;
                var error = RunCommand(ffmpeg, plan, plan.Commands[index], isLast, onProgress, cancellationToken);

                if (error != null)
                {
                    CleanupOutput(plan.OutputPath);

                    return Failure(error);
                }
            }

            var outputExists = _diskProvider.FileExists(plan.OutputPath);
            var outputSize = outputExists ? _diskProvider.GetFileSize(plan.OutputPath) : 0;

            // A "successful" ffmpeg exit that produced nothing must never be treated as a real output;
            // the resolver's overwrite path would otherwise replace a good file with an empty one.
            if (outputSize <= 0)
            {
                CleanupOutput(plan.OutputPath);

                return Failure("ffmpeg produced an empty output file");
            }

            return new TranscodeResult
            {
                Success = true,
                OutputPath = plan.OutputPath,
                OutputSize = outputSize
            };
        }

        private string RunCommand(string ffmpeg, TranscodePlan plan, string arguments, bool reportProgress, Action<TranscodeProgress> onProgress, CancellationToken cancellationToken)
        {
            var parser = new TranscodeProgressParser(plan.DurationSeconds);
            var errors = new Queue<string>();
            var argumentsWithGlobals = "-hide_banner -nostdin -y " + arguments;
            var nice = GetNice(plan.Device);
            var absoluteTimeout = GetCommandTimeout(plan.DurationSeconds);

            // Any output (the -progress stream on stdout, or ffmpeg's status/log lines on stderr) is a
            // liveness signal; the deadline is reset on every callback rather than measuring total
            // wall-clock, so a legitimately slow encode is not killed mid-flight.
            var startedUtc = DateTime.UtcNow;
            var lastActivityTicks = startedUtc.Ticks;

            void RecordActivity()
            {
                Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);
            }

            Process process;

            try
            {
                process = _processProvider.Start(
                    GetLaunchPath(ffmpeg, nice),
                    GetLaunchArguments(ffmpeg, argumentsWithGlobals, nice),
                    null,
                    line =>
                    {
                        RecordActivity();

                        if (!reportProgress)
                        {
                            return;
                        }

                        var progress = parser.Parse(line);

                        if (progress != null)
                        {
                            onProgress?.Invoke(progress);
                        }
                    },
                    line =>
                    {
                        RecordActivity();

                        lock (errors)
                        {
                            errors.Enqueue(line);

                            while (errors.Count > ErrorTailLength)
                            {
                                errors.Dequeue();
                            }
                        }
                    },
                    logOutput: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to start ffmpeg for transcode job {0}", plan.Job.Id);
                return ex.Message;
            }

            var timedOut = false;
            var timedOutForInactivity = false;

            try
            {
                using (cancellationToken.Register(() => Kill(process, plan.Job.Id)))
                {
                    while (!process.WaitForExit(WaitPollMilliseconds))
                    {
                        // The token callback kills the process, but a Kill that fails (or a process that
                        // refuses to die) must not spin until the absolute timeout: observe the token in
                        // the loop and stop waiting regardless.
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Kill(process, plan.Job.Id);
                            break;
                        }

                        var now = DateTime.UtcNow;
                        var lastActivityUtc = new DateTime(Interlocked.Read(ref lastActivityTicks));

                        if (HasTimedOut(startedUtc, lastActivityUtc, now, InactivityTimeout, absoluteTimeout, out timedOutForInactivity))
                        {
                            timedOut = true;
                            Kill(process, plan.Job.Id);
                            break;
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return "Cancelled";
                }

                if (timedOut)
                {
                    return timedOutForInactivity
                        ? $"ffmpeg made no progress for {InactivityTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s"
                        : $"ffmpeg exceeded the absolute time limit of {absoluteTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s";
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    lock (errors)
                    {
                        return errors.LastOrDefault() ?? $"ffmpeg exited with code {process.ExitCode}";
                    }
                }

                return null;
            }
            finally
            {
                // The Process handle (and its pipes) is owned by this method: returning without
                // disposing leaks the handle for the lifetime of the process, once per command.
                process.Dispose();
            }
        }

        // Best-effort CPU niceness: only a software device carries CPU options and only Unix has the
        // nice(1) wrapper. Hardware and remux work run ffmpeg directly. An explicit non-zero per-device
        // Nice wins; otherwise the global TranscodeNice setting applies.
        public int GetNice(TranscodeDevice device)
        {
            return GetNice(device, _configService.TranscodeNice);
        }

        public static int GetNice(TranscodeDevice device, int globalNice)
        {
            if (device == null || device.Kind != TranscodeDeviceKind.Software)
            {
                return 0;
            }

            var explicitNice = device.Options?.Cpu?.Nice ?? 0;

            return explicitNice != 0 ? explicitNice : globalNice;
        }

        public static string GetLaunchPath(string ffmpeg, int nice)
        {
            return OsInfo.IsNotWindows && nice != 0 ? "nice" : ffmpeg;
        }

        public static string GetLaunchArguments(string ffmpeg, string arguments, int nice)
        {
            if (!OsInfo.IsNotWindows || nice == 0)
            {
                return arguments;
            }

            return $"-n {nice.ToString(CultureInfo.InvariantCulture)} {TranscodeArgumentBuilder.Quote(ffmpeg)} {arguments}";
        }

        // True once either the inactivity window (no output at all) or the absolute ceiling is hit;
        // "inactivity" distinguishes the two so the caller can report which deadline fired.
        public static bool HasTimedOut(DateTime startedUtc, DateTime lastActivityUtc, DateTime nowUtc, TimeSpan inactivityTimeout, TimeSpan absoluteTimeout, out bool inactivity)
        {
            if (nowUtc - startedUtc >= absoluteTimeout)
            {
                inactivity = false;
                return true;
            }

            if (nowUtc - lastActivityUtc >= inactivityTimeout)
            {
                inactivity = true;
                return true;
            }

            inactivity = false;
            return false;
        }

        public static TimeSpan GetCommandTimeout(double durationSeconds)
        {
            if (!(durationSeconds > 0))
            {
                // With no duration (a failed probe) the only safe absolute ceiling is the maximum;
                // the progress-based inactivity window is what actually kills a stalled encode, so a
                // long movie with an unknown duration is not cut off at the old fixed 2 hours.
                return MaximumCommandTimeout;
            }

            var timeout = TimeSpan.FromSeconds(durationSeconds * DurationTimeoutFactor);

            if (timeout < MinimumCommandTimeout)
            {
                return MinimumCommandTimeout;
            }

            if (timeout > MaximumCommandTimeout)
            {
                return MaximumCommandTimeout;
            }

            return timeout;
        }

        private void Kill(Process process, int jobId)
        {
            try
            {
                process.Kill(true);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to kill ffmpeg for transcode job {0}", jobId);
            }
        }

        private void CleanupOutput(string outputPath)
        {
            try
            {
                if (_diskProvider.FileExists(outputPath))
                {
                    _diskProvider.DeleteFile(outputPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to delete partial transcode output {0}", outputPath);
            }
        }

        private static TranscodeResult Failure(string error)
        {
            return new TranscodeResult
            {
                Success = false,
                Error = error
            };
        }
    }
}
