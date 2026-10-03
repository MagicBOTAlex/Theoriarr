using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeService
    {
        TranscodeJob Queue(TranscodeJob job);
        void Start();
        void Wake();
        void ExecuteJob(TranscodeJob job, TranscodeDevice device);
        CancelJobResult CancelJob(int jobId);
        List<TranscodeJob> GetJobsExceedingSettings();
        List<TranscodeJob> ForceStopJobs(IEnumerable<int> jobIds);
        int ClearHistory();
        TranscodeJob Resolve(int jobId, TranscodeReviewAction action);
        List<TranscodeResolveResult> ResolveJobs(IEnumerable<int> jobIds, TranscodeReviewAction action);
        string GetTempFolder();
    }

    // Owns the durable job rows, the pre-flight guards and the device scheduler. Concurrency is
    // bounded globally (MaxConcurrentJobs) and per device (TranscodeDevice.MaxParallel); ffmpeg work
    // never runs on the shared CommandExecutor threads.
    public class TranscodeService : ITranscodeService, IHandle<ApplicationStartedEvent>, IExecute<TranscodeMediaCommand>
    {
        public const double OverheadRatio = 0.05;
        public const double FreeSpaceMarginRatio = 0.05;
        public const double QualityWorkingSpaceFactor = 1.5;
        public const string RemuxDeviceId = "remux";
        internal const int LockStripeCount = 64;

        private static readonly StringComparison PathComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private readonly object _activeLock = new object();
        private readonly Dictionary<int, string> _active = new Dictionary<int, string>();
        private readonly Dictionary<int, CancellationTokenSource> _cancellations = new Dictionary<int, CancellationTokenSource>();
        private readonly Dictionary<int, DateTime> _scheduleFailures = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _pendingFailures = new Dictionary<int, DateTime>();

        // Runtime device failures: a device that just failed an encode is skipped for a short window
        // so a requeue lands on another capable device instead of repeating the same failure. The
        // cooldown is in-memory only (a restart clears it) and never marks the device permanently
        // unavailable, so a single bad source cannot take a healthy GPU out of service for good.
        private readonly Dictionary<string, DateTime> _deviceCooldowns = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object _deviceCooldownLock = new object();

        // Difficulty (estimated pixels to encode) per queued job: probing is done once and the score
        // is reused across scheduler passes, then pruned to the live queue.
        private readonly Dictionary<int, double> _difficultyCache = new Dictionary<int, double>();
        private readonly object _difficultyLock = new object();

        // Fixed striped locks (bounded memory) so a double-click or a per-row resolve overlapping a
        // bulk resolve cannot run the finalize path twice, and so the claim/cancel/finish status
        // transitions for one job id are serialized without ever holding a lock across an encode.
        private readonly object[] _resolveLocks = CreateLocks();
        private readonly object[] _transitionLocks = CreateLocks();
        private readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);

        private readonly ITranscodeJobRepository _repository;
        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly ITranscodeRunner _runner;
        private readonly ITranscodeArgumentBuilder _argumentBuilder;
        private readonly IGpuCapabilityService _gpuCapabilityService;
        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMovieFileService _movieFileService;
        private readonly Logger _logger;

        private bool _started;
        private DateTime _lastReconcileUtc = DateTime.MinValue;

        public TranscodeService(ITranscodeJobRepository repository,
                                IConfigService configService,
                                IAppFolderInfo appFolderInfo,
                                IDiskProvider diskProvider,
                                ITranscodeRunner runner,
                                ITranscodeArgumentBuilder argumentBuilder,
                                IGpuCapabilityService gpuCapabilityService,
                                IVideoFileInfoReader videoFileInfoReader,
                                IEventAggregator eventAggregator,
                                IDiskTransferService diskTransferService,
                                IRecycleBinProvider recycleBinProvider,
                                IMediaFileService mediaFileService,
                                IMovieFileService movieFileService,
                                Logger logger)
        {
            _repository = repository;
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _runner = runner;
            _argumentBuilder = argumentBuilder;
            _gpuCapabilityService = gpuCapabilityService;
            _videoFileInfoReader = videoFileInfoReader;
            _eventAggregator = eventAggregator;
            _diskTransferService = diskTransferService;
            _recycleBinProvider = recycleBinProvider;
            _mediaFileService = mediaFileService;
            _movieFileService = movieFileService;
            _logger = logger;
        }

        // Test hooks: the fixture shortens the scheduling grace and transfer-watcher bound and uses
        // the claim callback to drive the cancel/claim window deterministically.
        internal TimeSpan ScheduleFailureGrace { get; set; } = TimeSpan.FromSeconds(60);

        // A separate, longer grace for the "possibly transient" case (empty snapshot, or a capable
        // device that is disabled/unavailable). A transient empty probe must not fail immediately,
        // but a permanently missing/disabled device must not leave the job Queued forever either.
        internal TimeSpan PendingDeviceGrace { get; set; } = TimeSpan.FromMinutes(30);
        internal TimeSpan TransferWatcherStopTimeout { get; set; } = TimeSpan.FromSeconds(5);
        internal Action<TranscodeJob> OnJobClaimed { get; set; }

        // How often the pump reconciles Running/Transferring rows that no in-memory worker owns,
        // and how long a row must be untouched before it is treated as stranded. The transfer
        // progress watcher refreshes LastUpdatedAt every 500 ms, so a live copy never ages out.
        internal TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromMinutes(1);
        internal TimeSpan StrandedJobGrace { get; set; } = TimeSpan.FromMinutes(2);

        // How long a device is skipped after a runtime encode failure.
        internal TimeSpan DeviceCooldown { get; set; } = TimeSpan.FromMinutes(5);

        public string GetTempFolder()
        {
            var configured = _configService.TranscodeTempFolder;

            if (configured.IsNotNullOrWhiteSpace())
            {
                return configured;
            }

            return Path.Combine(_appFolderInfo.AppDataFolder, "transcode");
        }

        public static long DeriveVideoBitrate(long targetBytes, double durationSeconds, long audioBitrateBps)
        {
            if (targetBytes <= 0 || durationSeconds <= 0)
            {
                return 0;
            }

            var totalBitrateBps = (long)(targetBytes * 8 / durationSeconds);
            var overheadBps = (long)(totalBitrateBps * OverheadRatio);

            return Math.Max(0, totalBitrateBps - Math.Max(0, audioBitrateBps) - overheadBps);
        }

        public static bool ShouldSkip(long sourceVideoBitrateBps, long targetVideoBitrateBps)
        {
            return sourceVideoBitrateBps > 0 && targetVideoBitrateBps > 0 && targetVideoBitrateBps >= sourceVideoBitrateBps;
        }

        public static TranscodeCodec ParseCodec(string codec)
        {
            return TranscodeCodecs.Parse(codec);
        }

        public static bool AllowsCodec(TranscodeDevice device, TranscodeCodec codec)
        {
            var name = TranscodeCodecs.Name(codec);
            var setting = device.Codecs?.FirstOrDefault(candidate => string.Equals(candidate.Codec, name, StringComparison.OrdinalIgnoreCase));

            if (setting != null)
            {
                // The per-codec toggle is authoritative: a codec the probe rejected is still usable when
                // the user forced it on, as long as an encoder can be resolved by convention.
                return setting.Enabled && TranscodeArgumentBuilder.GetEncoderFor(device, codec, allowUnsupported: true) != null;
            }

            // No per-codec settings (legacy stored device or hand-built test device): fall back to the
            // detected capability.
            return device.Supported && TranscodeArgumentBuilder.GetEncoderFor(device, codec) != null;
        }

        public static TranscodeDevice SelectDevice(List<TranscodeDevice> devices, TranscodeCodec codec, IDictionary<string, int> running, string preferredDeviceId)
        {
            return SelectDevice(devices, codec, running, preferredDeviceId, preferHardware: false);
        }

        public static TranscodeDevice SelectDevice(List<TranscodeDevice> devices, TranscodeCodec codec, IDictionary<string, int> running, string preferredDeviceId, bool preferHardware)
        {
            var capable = (devices ?? new List<TranscodeDevice>())
                .Where(device => device.Enabled && !device.Unavailable)
                .Where(device => AllowsCodec(device, codec))
                .Where(device => !running.TryGetValue(device.Id, out var count) || count < Math.Max(1, device.MaxParallel));

            // Hardware is only preferred while it still has a suitable encoder; a disabled or
            // saturated GPU falls through to the existing priority ordering (software remains usable).
            var candidates = preferHardware
                ? capable.OrderByDescending(device => device.Kind != TranscodeDeviceKind.Software)
                          .ThenBy(device => device.Priority)
                          .ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
                          .ToList()
                : capable.OrderBy(device => device.Priority)
                          .ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
                          .ToList();

            return candidates.FirstOrDefault(device => device.Id == preferredDeviceId) ?? candidates.FirstOrDefault();
        }

        public bool HasEnoughFreeSpace(long sourceSize)
        {
            var available = _diskProvider.GetAvailableSpace(GetTempFolder());

            if (available == null)
            {
                return true;
            }

            return available.Value >= sourceSize + (long)(sourceSize * FreeSpaceMarginRatio);
        }

        public TranscodeJob Queue(TranscodeJob job)
        {
            if (!_configService.MediaCompressionEnabled)
            {
                throw new InvalidOperationException("Media compression is disabled");
            }

            job.Status = TranscodeJobStatus.Queued;
            job.Progress = 0;
            job.LastUpdatedAt = DateTime.UtcNow;

            return _repository.Insert(job);
        }

        public void Start()
        {
            lock (_activeLock)
            {
                if (_started)
                {
                    return;
                }

                _started = true;
            }

            var thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "TranscodeScheduler"
            };

            thread.Start();
        }

        // Starts the scheduler if needed and nudges it so settings changes (a device enabled, more
        // parallelism, a new job) take effect immediately instead of waiting for the next tick.
        public void Wake()
        {
            Start();

            // Device settings may have changed (UpdateDevices calls Wake), so give every queued job a
            // fresh scheduling grace before the next pump tick.
            lock (_activeLock)
            {
                _scheduleFailures.Clear();
                _pendingFailures.Clear();
            }

            try
            {
                if (_wake.CurrentCount == 0)
                {
                    _wake.Release();
                }
            }
            catch (SemaphoreFullException)
            {
            }
        }

        public void Handle(ApplicationStartedEvent message)
        {
            try
            {
                RecoverInterruptedJobs();
            }
            catch (Exception ex)
            {
                // A single unreadable row must never stop the scheduler from starting.
                _logger.Error(ex, "Unable to recover interrupted transcode jobs");
            }

            Start();
        }

        public TranscodeJob Resolve(int jobId, TranscodeReviewAction action)
        {
            if (action == TranscodeReviewAction.Discard)
            {
                return DiscardReview(jobId);
            }

            return FinishTransfer(ClaimTransfer(jobId), action);
        }

        private TranscodeJob DiscardReview(int jobId)
        {
            lock (GetResolveLock(jobId))
            {
                var discarded = LoadReviewable(jobId);
                DeleteQuietly(discarded.OutputPath);
                Complete(discarded);

                return discarded;
            }
        }

        // Phase 1 (fast) under the per-job stripe gate: validate, claim the review by flipping the
        // row to Transferring, and compute the paths. Because the row is no longer AwaitingReview
        // once the gate is released, a concurrent double resolve is rejected, so the multi-GB copy
        // below can run without holding the stripe (R3-E6). Split out of Resolve so a bulk resolve
        // can claim every job up front (see ResolveJobs).
        private TransferClaim ClaimTransfer(int jobId)
        {
            lock (GetResolveLock(jobId))
            {
                var job = LoadReviewable(jobId);

                if (job.OutputPath.IsNullOrWhiteSpace() || !_diskProvider.FileExists(job.OutputPath))
                {
                    throw new InvalidOperationException("The transcoded output file is missing");
                }

                string destination;

                try
                {
                    destination = GetDestinationPath(job);
                }
                catch (Exception ex)
                {
                    // A hostile Container/Tag must not surface as a bare API error: fail the job's
                    // resolve with a clear message and keep it in review so the user can fix or discard.
                    job.Error = $"Unable to determine the transcode destination: {ex.Message}";
                    job.LastUpdatedAt = DateTime.UtcNow;
                    Persist(job);
                    Publish(job);

                    throw new InvalidOperationException(job.Error, ex);
                }

                var outputSize = GetFileSizeQuietly(job.OutputPath);

                if (outputSize <= 0)
                {
                    // Never let a degenerate encode replace a good library file with an empty one.
                    job.Error = "The transcoded output is empty";
                    job.LastUpdatedAt = DateTime.UtcNow;
                    Persist(job);
                    Publish(job);

                    throw new InvalidOperationException(job.Error);
                }

                if (IsCrossMount(job.OutputPath, destination) && !HasEnoughSpaceAt(destination, outputSize))
                {
                    job.Error = $"Not enough free space at {Path.GetDirectoryName(destination)} to place the transcoded file";
                    job.LastUpdatedAt = DateTime.UtcNow;
                    Persist(job);
                    Publish(job);

                    throw new InvalidOperationException(job.Error);
                }

                // Copy the finished file next to its destination first; the original is only removed
                // once the replacement is in place, so a failed transfer (e.g. a full library disk)
                // can never lose the source file.
                var staging = GetStagingPath(destination, job.Id);
                DeleteQuietly(staging);

                // Moving the finished file into the library can be a cross-volume copy of many GB,
                // so surface it as its own status with byte progress while it runs.
                job.Status = TranscodeJobStatus.Transferring;
                job.Progress = 0;
                job.Speed = null;
                job.Fps = null;
                job.Eta = null;
                job.Error = null;
                job.LastUpdatedAt = DateTime.UtcNow;

                // This write is the mutual-exclusion token for the whole transfer: if it cannot be
                // persisted the row is not claimed, so abort instead of copying a second time.
                if (!TryPersistRequired(job))
                {
                    job.Status = TranscodeJobStatus.AwaitingReview;
                    job.Error = "Unable to persist the transfer state";
                    throw new InvalidOperationException(job.Error);
                }

                Publish(job);

                return new TransferClaim(job, destination, staging, outputSize);
            }
        }

        // Phase 2 (slow) runs without the stripe gate: the T7 double-finalize guarantee is kept
        // because the row is Transferring for the whole copy, so a second Resolve is rejected by
        // LoadReviewable rather than waiting on (or sharing) this transfer's stripe.
        private TranscodeJob FinishTransfer(TransferClaim claim, TranscodeReviewAction action)
        {
            var job = claim.Job;

            // The transfer copy may run for many minutes; register its cancellation source under the
            // job id (the encode worker has already released it) so CancelJob can abort the copy.
            var transferCancellation = new CancellationTokenSource();

            lock (_activeLock)
            {
                _cancellations[job.Id] = transferCancellation;
            }

            try
            {
                TransferOutput(job, claim.Staging, claim.OutputSize, transferCancellation.Token);

                if (transferCancellation.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                if (action == TranscodeReviewAction.KeepBoth)
                {
                    if (string.Equals(claim.Destination, job.SourcePath, PathComparison))
                    {
                        var original = GetKeepBothPath(job.SourcePath);
                        _diskTransferService.TransferFile(job.SourcePath, original, TransferMode.Move);
                        job.OriginalPath = original;
                    }
                    else
                    {
                        // A tagged/renamed output lands beside the untouched original, so there is no
                        // collision to resolve.
                        job.OriginalPath = job.SourcePath;
                    }

                    _diskTransferService.TransferFile(claim.Staging, claim.Destination, TransferMode.Move, overwrite: true);
                }
                else
                {
                    ReplaceOriginal(claim, job);
                }
            }
            catch (OperationCanceledException)
            {
                // The user aborted the copy: drop the partial staging file but keep the finished
                // output so the job can simply be resolved (or discarded) again.
                DeleteQuietly(claim.Staging);

                lock (GetResolveLock(job.Id))
                {
                    job.Status = TranscodeJobStatus.AwaitingReview;
                    job.Progress = 100;
                    job.Speed = null;
                    job.Error = "Transfer cancelled; the transcoded output was kept for review";
                    job.LastUpdatedAt = DateTime.UtcNow;
                    Persist(job);
                    Publish(job);
                }

                throw;
            }
            catch (Exception ex)
            {
                DeleteQuietly(claim.Staging);

                lock (GetResolveLock(job.Id))
                {
                    // The source output is kept so the job can simply be resolved again.
                    job.Status = TranscodeJobStatus.AwaitingReview;
                    job.Progress = 100;
                    job.Speed = null;
                    job.Error = ex.Message;
                    job.LastUpdatedAt = DateTime.UtcNow;
                    Persist(job);
                    Publish(job);
                }

                throw;
            }
            finally
            {
                lock (_activeLock)
                {
                    _cancellations.Remove(job.Id);
                }

                transferCancellation.Dispose();
            }

            // Phase 3: the terminal transition is gated again so a second resolve cannot finalize
            // the same job twice (it would have been rejected as Transferring during phase 2).
            lock (GetResolveLock(job.Id))
            {
                job.Progress = 100;
                job.Error = null;

                UpdateLibraryFile(job, claim.Destination);

                Complete(job);
            }

            return job;
        }

        // Places the staged replacement and only then discards the original. For an in-place replace
        // (destination == source) the original is moved to a sibling first and restored if the final
        // move fails, so a failed placement never loses the only copy. For a renamed/tagged output the
        // original is recycled after the replacement has landed.
        private void ReplaceOriginal(TransferClaim claim, TranscodeJob job)
        {
            if (string.Equals(claim.Destination, job.SourcePath, PathComparison))
            {
                var original = claim.Destination + ".transcode-original~";
                DeleteQuietly(original);
                _diskTransferService.TransferFile(claim.Destination, original, TransferMode.Move, overwrite: true);

                try
                {
                    _diskTransferService.TransferFile(claim.Staging, claim.Destination, TransferMode.Move, overwrite: true);
                }
                catch
                {
                    // Put the original back so a failed placement does not lose it; any partial
                    // replacement is removed first.
                    DeleteQuietly(claim.Destination);

                    if (_diskProvider.FileExists(original))
                    {
                        _diskTransferService.TransferFile(original, claim.Destination, TransferMode.Move, overwrite: true);
                    }

                    throw;
                }

                // The replacement is in place; only now discard the original.
                _recycleBinProvider.DeleteFile(original);
                return;
            }

            if (_diskProvider.FileExists(claim.Destination))
            {
                // A different container target can already exist (e.g. an .mkv beside an .mp4);
                // recycle it before the overwrite so it is not silently destroyed.
                _recycleBinProvider.DeleteFile(claim.Destination);
            }

            _diskTransferService.TransferFile(claim.Staging, claim.Destination, TransferMode.Move, overwrite: true);

            // The replacement is in place; only now discard the original.
            _recycleBinProvider.DeleteFile(job.SourcePath);
        }

        private sealed class TransferClaim
        {
            public TransferClaim(TranscodeJob job, string destination, string staging, long outputSize)
            {
                Job = job;
                Destination = destination;
                Staging = staging;
                OutputSize = outputSize;
            }

            public TranscodeJob Job { get; }

            public string Destination { get; }

            public string Staging { get; }

            public long OutputSize { get; }
        }

        // Fixed-size striped gates: per-job serialization with memory bounded by the stripe count
        // rather than the number of jobs ever seen.
        private static object[] CreateLocks()
        {
            var locks = new object[LockStripeCount];

            for (var index = 0; index < locks.Length; index++)
            {
                locks[index] = new object();
            }

            return locks;
        }

        internal object GetResolveLock(int jobId)
        {
            return _resolveLocks[(int)((uint)jobId % (uint)_resolveLocks.Length)];
        }

        private object GetTransitionLock(int jobId)
        {
            return _transitionLocks[(int)((uint)jobId % (uint)_transitionLocks.Length)];
        }

        private TranscodeJob LoadReviewable(int jobId)
        {
            var job = _repository.Get(jobId);

            if (job == null)
            {
                throw new InvalidOperationException($"Transcode job {jobId} was not found");
            }

            if (job.Status != TranscodeJobStatus.AwaitingReview)
            {
                throw new InvalidOperationException($"Transcode job {jobId} is not awaiting review");
            }

            return job;
        }

        // Polls the destination size while the file is copied so the UI can show transfer progress.
        private Task WatchTransfer(TranscodeJob job, string destination, long totalSize, CancellationToken cancellationToken)
        {
            if (totalSize <= 0 || destination.IsNullOrWhiteSpace())
            {
                return Task.CompletedTask;
            }

            // The token is intentionally not passed to Task.Run: the caller disposes the source
            // after its bounded stop wait, and a watcher already running (or abandoned) must not
            // fault scheduling or throw out of the task on a disposed source.
            return Task.Run(
                () =>
                {
                    var lastUpdate = DateTime.MinValue;
                    var lastWritten = 0L;
                    var lastSampleUtc = DateTime.UtcNow;

                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                if (_diskProvider.FileExists(destination))
                                {
                                    var written = _diskProvider.GetFileSize(destination);
                                    var percent = Math.Min(99, written * 100.0 / totalSize);
                                    var now = DateTime.UtcNow;

                                    if (percent > job.Progress && (now - lastUpdate).TotalMilliseconds >= 500)
                                    {
                                        var elapsed = (now - lastSampleUtc).TotalSeconds;
                                        var speed = elapsed > 0.1 && written > lastWritten
                                            ? FormatTransferSpeed((written - lastWritten) / elapsed)
                                            : null;

                                        lastWritten = written;
                                        lastSampleUtc = now;

                                        // Cheap early-out: the resolve may have completed while the
                                        // watcher was reading the size.
                                        var persisted = _repository.Find(job.Id);

                                        if (persisted == null || persisted.Status != TranscodeJobStatus.Transferring)
                                        {
                                            return;
                                        }

                                        // The authoritative re-check and the write share the per-job
                                        // transition gate with Complete, so the early-out cannot be
                                        // stale: once Complete has run the shared status is no longer
                                        // Transferring and a terminal row's progress is never lowered.
                                        lock (GetTransitionLock(job.Id))
                                        {
                                            if (job.Status != TranscodeJobStatus.Transferring)
                                            {
                                                return;
                                            }

                                            job.Progress = percent;

                                            if (speed != null)
                                            {
                                                job.Speed = speed;
                                            }

                                            job.LastUpdatedAt = DateTime.UtcNow;
                                            Persist(job);
                                            Publish(job);
                                        }

                                        lastUpdate = now;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Debug(ex, "Unable to read transfer progress for job {0}", job.Id);
                            }

                            if (cancellationToken.WaitHandle.WaitOne(500))
                            {
                                return;
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // The caller disposed the stop source after its bounded wait; stop quietly.
                    }
                },
                CancellationToken.None);
        }

        private static string FormatTransferSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
            {
                return null;
            }

            var mib = bytesPerSecond / (1024.0 * 1024.0);

            if (mib >= 1)
            {
                return mib.ToString("0.0", CultureInfo.InvariantCulture) + " MB/s";
            }

            return (bytesPerSecond / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB/s";
        }

        private long GetFileSizeQuietly(string path)
        {
            try
            {
                return path.IsNotNullOrWhiteSpace() && _diskProvider.FileExists(path)
                    ? _diskProvider.GetFileSize(path)
                    : 0;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read file size for {0}", path);
                return 0;
            }
        }

        // Copies the finished output to the staging file (a normal, verified transfer that can span
        // volumes) while reporting byte progress. The output itself is left in place so a failure or
        // restart can retry without re-encoding. The external token aborts the copy (CancelJob).
        private void TransferOutput(TranscodeJob job, string staging, long totalSize, CancellationToken cancellationToken)
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var transferProgress = WatchTransfer(job, staging, totalSize, stop.Token);

            try
            {
                _diskTransferService.TransferFile(job.OutputPath, staging, TransferMode.Copy, true, cancellationToken);
            }
            finally
            {
                stop.Cancel();

                // Await the watcher so it cannot persist a stale Transferring/progress state after
                // the caller has moved on to Complete, but never block the resolve HTTP request
                // forever on a stuck repository/IO call: the watcher re-checks the status under the
                // transition gate, so a late update cannot lower a terminal row's progress.
                try
                {
                    if (!transferProgress.Wait(TransferWatcherStopTimeout, CancellationToken.None))
                    {
                        _logger.Warn("Transfer progress watcher for job {0} did not stop within {1}s", job.Id, TransferWatcherStopTimeout.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Transfer progress watcher for job {0} did not stop cleanly", job.Id);
                }
            }
        }

        // Unique per job: two jobs for the same destination (e.g. a requeue overlapping a resolve)
        // must never share a staging file, or their copies would corrupt each other.
        private static string GetStagingPath(string path, int jobId)
        {
            return $"{path}.{jobId}.transcode~";
        }

        private bool IsCrossMount(string sourcePath, string targetPath)
        {
            var sourceMount = _diskProvider.GetMount(sourcePath);
            var targetMount = _diskProvider.GetMount(targetPath);

            return sourceMount != null && targetMount != null &&
                   sourceMount.RootDirectory != targetMount.RootDirectory;
        }

        private bool HasEnoughSpaceAt(string targetPath, long size)
        {
            var available = _diskProvider.GetAvailableSpace(Path.GetDirectoryName(targetPath));

            if (available == null)
            {
                return true;
            }

            return available.Value >= size + (long)(size * FreeSpaceMarginRatio);
        }

        public List<TranscodeResolveResult> ResolveJobs(IEnumerable<int> jobIds, TranscodeReviewAction action)
        {
            var ids = (jobIds ?? Enumerable.Empty<int>()).ToList();
            var results = new List<TranscodeResolveResult>();

            if (action == TranscodeReviewAction.Discard)
            {
                foreach (var jobId in ids)
                {
                    try
                    {
                        results.Add(new TranscodeResolveResult(jobId, DiscardReview(jobId), null));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unable to resolve transcode job {0}", jobId);
                        results.Add(new TranscodeResolveResult(jobId, null, ex.Message));
                    }
                }

                return results;
            }

            // Claim every review up front (fast) so the whole batch leaves the review list before any
            // of the multi-GB copies start: otherwise a page refresh mid-batch would still show the
            // not-yet-processed jobs as reviewable and let them be started again.
            var claims = new List<TransferClaim>();

            foreach (var jobId in ids)
            {
                try
                {
                    claims.Add(ClaimTransfer(jobId));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to resolve transcode job {0}", jobId);
                    results.Add(new TranscodeResolveResult(jobId, null, ex.Message));
                }
            }

            foreach (var claim in claims)
            {
                try
                {
                    results.Add(new TranscodeResolveResult(claim.Job.Id, FinishTransfer(claim, action), null));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to resolve transcode job {0}", claim.Job.Id);
                    results.Add(new TranscodeResolveResult(claim.Job.Id, null, ex.Message));
                }
            }

            return results;
        }

        public void Execute(TranscodeMediaCommand message)
        {
            Start();
        }

        public void ExecuteJob(TranscodeJob job, TranscodeDevice device)
        {
            ExecuteJob(job, device, CancellationToken.None);
        }

        public CancelJobResult CancelJob(int jobId)
        {
            // Take the same per-job gate as the Queued->Running claim so the two cannot interleave:
            // a cancel either wins (and the claim re-reads Cancelled) or observes the registered CTS
            // and cancels the encode. A lock is never held across the encode itself.
            lock (GetTransitionLock(jobId))
            {
                // The lookup and the cancel share _activeLock with the worker's teardown, which
                // removes and disposes the source under the same lock: the source can never be
                // disposed between reading it and cancelling it. The ObjectDisposedException guard
                // is belt-and-braces for a source disposed by any other path.
                lock (_activeLock)
                {
                    if (_cancellations.TryGetValue(jobId, out var cancellation))
                    {
                        try
                        {
                            cancellation.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }

                        // A live encode or transfer is being aborted; its worker flips the row to
                        // Cancelled (or back to review) once it observes the token.
                        return CancelJobResult.Cancelled;
                    }
                }

                var job = _repository.Find(jobId);

                if (job == null)
                {
                    return CancelJobResult.NotFound;
                }

                if (job.Status != TranscodeJobStatus.Queued)
                {
                    return CancelJobResult.NotCancellable;
                }

                Finish(job, TranscodeJobStatus.Cancelled);

                return CancelJobResult.Cancelled;
            }
        }

        public void ExecuteJob(TranscodeJob job, TranscodeDevice device, CancellationToken cancellationToken)
        {
            job = _repository.Find(job.Id);

            // The scheduler snapshots Queued rows before claiming them; a cancel or another claim in
            // that window must win, so re-read the persisted row before doing any work.
            if (job == null || job.Status != TranscodeJobStatus.Queued)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Finish(job, TranscodeJobStatus.Cancelled);
                return;
            }

            string outputPath;
            string destinationPath;

            try
            {
                outputPath = GetOutputPath(job);
                destinationPath = GetDestinationPath(job);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to derive the transcode paths for job {0}", job.Id);
                Finish(job, TranscodeJobStatus.Failed, error: ex.Message);
                return;
            }

            var mediaInfo = SafeProbe(job.SourcePath);
            var duration = mediaInfo?.RunTime.TotalSeconds ?? 0;
            var sourceVideoBitrate = mediaInfo?.VideoBitrate ?? 0;
            var audioBitrate = mediaInfo?.AudioStreams?.Sum(stream => stream.Bitrate) ?? 0;
            var sourceSize = job.SourceSize ?? 0;
            var codec = ParseCodec(job.VideoCodec);

            var targetBytes = ResolveTargetBytes(job, sourceSize);
            var videoBitrate = job.Mode == TranscodeMode.Quality || duration <= 0
                ? 0
                : DeriveVideoBitrate(targetBytes, duration, audioBitrate);

            if (job.Mode == TranscodeMode.Remux && string.Equals(destinationPath, job.SourcePath, PathComparison))
            {
                Finish(job, TranscodeJobStatus.Skipped, message: "Source is already in the target container");
                return;
            }

            if (job.Mode != TranscodeMode.Quality && job.Mode != TranscodeMode.Remux && videoBitrate <= 0)
            {
                Finish(job, TranscodeJobStatus.Failed, error: "Unable to derive a positive target video bitrate for this file");
                return;
            }

            if (job.Mode != TranscodeMode.Quality && job.Mode != TranscodeMode.Remux && ShouldSkip(sourceVideoBitrate, videoBitrate))
            {
                Finish(job, TranscodeJobStatus.Skipped, message: "Target bitrate is not smaller than the source");
                return;
            }

            // Quality/CRF can produce an output larger than the source and a two-pass run also writes
            // an ffmpeg2pass log, so require more headroom than the raw source size for those cases.
            var requiredWorkingSpace = job.Mode == TranscodeMode.Quality
                ? (long)(sourceSize * QualityWorkingSpaceFactor)
                : sourceSize;

            if (!HasEnoughFreeSpace(requiredWorkingSpace))
            {
                Finish(job, TranscodeJobStatus.Failed, error: "Not enough free space in the transcode working folder");
                return;
            }

            TranscodePlan plan;

            try
            {
                plan = _argumentBuilder.Build(job, device, codec, videoBitrate, duration, mediaInfo, outputPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to build the transcode plan for job {0}", job.Id);
                Finish(job, TranscodeJobStatus.Failed, error: ex.Message);
                return;
            }

            // Last check before claiming the slot: a cancel that landed during the (possibly slow)
            // probe/plan build must still abort the encode.
            if (cancellationToken.IsCancellationRequested)
            {
                Finish(job, TranscodeJobStatus.Cancelled);
                return;
            }

            // Claim the slot under the per-job gate: a cancel that landed while the probe/plan was
            // built must win, and a stale claim must not resurrect a terminal row. The encode is run
            // outside the lock.
            lock (GetTransitionLock(job.Id))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Finish(job, TranscodeJobStatus.Cancelled);
                    return;
                }

                var persisted = _repository.Find(job.Id);

                if (persisted == null || persisted.Status != TranscodeJobStatus.Queued)
                {
                    return;
                }

                job.Status = TranscodeJobStatus.Running;
                job.DeviceId = device?.Id;
                job.StartedAt = DateTime.UtcNow;
                job.LastUpdatedAt = DateTime.UtcNow;
                _repository.Update(job);
                Publish(job);
            }

            var lastUpdate = DateTime.UtcNow;

            void OnProgress(TranscodeProgress progress)
            {
                if (!progress.Completed && (DateTime.UtcNow - lastUpdate).TotalSeconds < 1)
                {
                    return;
                }

                lastUpdate = DateTime.UtcNow;

                // Share the per-job transition gate with the finalize/cancel writes and re-check the
                // persisted status, so a progress tick that loses the race cannot resurrect a row
                // that has already moved on (the same guard WatchTransfer uses).
                lock (GetTransitionLock(job.Id))
                {
                    var persisted = _repository.Find(job.Id);

                    if (persisted == null || persisted.Status != TranscodeJobStatus.Running)
                    {
                        return;
                    }

                    job.Progress = progress.Percent;
                    job.Speed = progress.Speed;
                    job.Fps = progress.Fps;
                    job.Eta = progress.Eta;
                    job.LastUpdatedAt = DateTime.UtcNow;

                    Persist(job);
                    Publish(job);
                }
            }

            TranscodeResult result;

            try
            {
                result = _runner.Run(plan, OnProgress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Transcode job {0} failed", job.Id);
                result = new TranscodeResult { Success = false, Error = ex.Message };
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Finish(job, TranscodeJobStatus.Cancelled);
                return;
            }

            if (!result.Success)
            {
                // A runtime encoder failure cools the device briefly so a requeue tries another.
                RecordDeviceFailure(job.DeviceId);
                Finish(job, TranscodeJobStatus.Failed, error: result.Error);
                return;
            }

            // Finalize under the per-job gate so a cancel that arrived while the encode was finishing
            // still wins over the AwaitingReview write.
            lock (GetTransitionLock(job.Id))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Finish(job, TranscodeJobStatus.Cancelled);
                    return;
                }

                job.Status = TranscodeJobStatus.AwaitingReview;
                job.OutputPath = result.OutputPath;
                job.OutputSize = result.OutputSize;
                job.Progress = 100;
                job.Error = null;
                job.EndedAt = DateTime.UtcNow;
                job.LastUpdatedAt = DateTime.UtcNow;
                Persist(job);
                Publish(job);
            }
        }

        private void Pump()
        {
            while (true)
            {
                try
                {
                    if (_configService.MediaCompressionEnabled)
                    {
                        // Periodically re-run recovery for rows the startup pass missed (e.g. an IO
                        // error) so a stranded Running/Transferring row is not stuck forever.
                        if (DateTime.UtcNow - _lastReconcileUtc >= ReconcileInterval)
                        {
                            _lastReconcileUtc = DateTime.UtcNow;
                            ReconcileStrandedJobs();
                        }

                        ScheduleOnce();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Transcode scheduler tick failed");
                }

                _wake.Wait(1000);
            }
        }

        internal void ScheduleOnce()
        {
            lock (_activeLock)
            {
                if (_active.Count >= Math.Max(1, _configService.MaxConcurrentJobs))
                {
                    return;
                }
            }

            var maxConcurrent = Math.Max(1, _configService.MaxConcurrentJobs);

            var queued = _repository.GetByStatus(TranscodeJobStatus.Queued)
                                    .OrderByDescending(job => job.Priority)
                                    .ThenBy(job => job.Id)
                                    .ToList();

            if (queued.Count == 0)
            {
                return;
            }

            // A device that recently failed an encode is left out of scheduling for a short window so
            // a requeue is placed on another capable device (the scheduler falls back when the pinned
            // device is absent, and HandleUnscheduledJob treats the job as pending, not blocked).
            var devices = (_gpuCapabilityService.GetDevices() ?? new List<TranscodeDevice>())
                .Where(device => !IsDeviceCooling(device.Id))
                .ToList();

            Dictionary<string, int> running;
            int activeCount;

            lock (_activeLock)
            {
                running = _active.Values.GroupBy(device => device).ToDictionary(group => group.Key, group => group.Count());
                activeCount = _active.Count;
            }

            // A single pass decides which job runs on which device: free slots are spread round-robin
            // across every idle device and the easiest jobs are paired with the least powerful (highest
            // priority-number) devices while the hardest go to the most powerful.
            var schedule = BuildSchedule(queued, devices, running, Math.Max(0, maxConcurrent - activeCount));

            var scheduledIds = new HashSet<int>();

            foreach (var entry in schedule)
            {
                var job = entry.Job;
                scheduledIds.Add(job.Id);

                // The snapshot above may already be stale: a cancel or another claim can land before
                // this row gets its turn.
                var current = _repository.Find(job.Id);

                if (current == null || current.Status != TranscodeJobStatus.Queued)
                {
                    continue;
                }

                ClearScheduleFailures(job.Id);

                var cancellation = new CancellationTokenSource();
                var claimed = false;

                // The claim is atomic with CancelJob: re-read the row under the same per-job gate the
                // cancel path holds, so a Queued->Cancelled finish and a Queued->Running claim cannot
                // interleave or overwrite each other.
                lock (GetTransitionLock(job.Id))
                {
                    var fresh = _repository.Find(job.Id);

                    if (fresh != null && fresh.Status == TranscodeJobStatus.Queued)
                    {
                        lock (_activeLock)
                        {
                            if (_active.Count < maxConcurrent && !_active.ContainsKey(job.Id))
                            {
                                _active[job.Id] = entry.DeviceKey;
                                _cancellations[job.Id] = cancellation;
                                claimed = true;
                            }
                        }
                    }
                }

                if (!claimed)
                {
                    cancellation.Dispose();
                    continue;
                }

                OnJobClaimed?.Invoke(job);

                var scheduled = job;
                var device = entry.Device;

                Task.Run(() =>
                {
                    try
                    {
                        ExecuteJob(scheduled, device, cancellation.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unhandled failure in transcode job {0}", scheduled.Id);
                    }
                    finally
                    {
                        // Remove and dispose under the same lock CancelJob uses to look up and
                        // cancel the source, so a concurrent cancel cannot hit a disposed token.
                        lock (_activeLock)
                        {
                            _active.Remove(scheduled.Id);
                            _cancellations.Remove(scheduled.Id);
                            cancellation.Dispose();
                        }
                    }
                });
            }

            // Everything left in the queue is either saturated or has no usable device; run the
            // wait-vs-fail bookkeeping for those.
            foreach (var job in queued)
            {
                if (scheduledIds.Contains(job.Id))
                {
                    continue;
                }

                HandleUnscheduledJob(job, devices);
            }

            PruneDifficultyCache(queued);
        }

        internal List<(TranscodeJob Job, TranscodeDevice Device, string DeviceKey)> BuildSchedule(
            List<TranscodeJob> queued,
            List<TranscodeDevice> devices,
            IDictionary<string, int> running,
            int budget)
        {
            var schedule = new List<(TranscodeJob Job, TranscodeDevice Device, string DeviceKey)>();

            if (budget <= 0)
            {
                return schedule;
            }

            // A higher Priority value wins, then FIFO by id, for every mode (the easiest-first pass
            // below only reorders within the same priority band).
            queued = (queued ?? new List<TranscodeJob>())
                .OrderByDescending(job => job.Priority)
                .ThenBy(job => job.Id)
                .ToList();

            // Default: walk the queue in order and fill each device up to its Max parallel (the
            // preferred device first). When "delegate easiest jobs first" is on, spread the free slots
            // across all devices and pair the easiest jobs with the least powerful ones instead.
            if (!_configService.TranscodeEasiestJobsFirst)
            {
                return BuildSequentialSchedule(queued, devices, running, budget);
            }

            var used = new HashSet<int>();
            var remaining = budget;

            var availableDevices = (devices ?? new List<TranscodeDevice>())
                .Where(device => device.Enabled && !device.Unavailable)
                .ToList();

            var encodeSchedule = new List<(TranscodeJob Job, TranscodeDevice Device, string DeviceKey)>();

            if (availableDevices.Count > 0)
            {
                // Most powerful first: hardware (when preferred), then lower priority numbers.
                var byStrength = availableDevices
                    .OrderByDescending(device => _configService.PreferHardware && device.Kind != TranscodeDeviceKind.Software)
                    .ThenBy(device => device.Priority)
                    .ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var strength = byStrength
                    .Select((device, index) => (device.Id, index))
                    .ToDictionary(entry => entry.Id, entry => entry.index);

                var freeSlots = byStrength.ToDictionary(
                    device => device.Id,
                    device => Math.Max(0, device.MaxParallel - (running != null && running.TryGetValue(device.Id, out var count) ? count : 0)));

                // Honour a pinned device first (a profile or the request can set one); fall back to the
                // general pairing when the pin is disabled, unavailable or saturated.
                foreach (var job in queued)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    if (used.Contains(job.Id) || job.Mode == TranscodeMode.Remux || job.DeviceId.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    var pinned = byStrength.FirstOrDefault(device => device.Id == job.DeviceId);

                    if (pinned == null || freeSlots[pinned.Id] <= 0 || !AllowsCodec(pinned, ParseCodec(job.VideoCodec)))
                    {
                        continue;
                    }

                    encodeSchedule.Add((job, pinned, pinned.Id));
                    used.Add(job.Id);
                    freeSlots[pinned.Id]--;
                    remaining--;
                }

                // Spread the free slots round-robin over the devices so an idle device gets a job before a
                // second one is packed onto an already-busy device.
                var slots = new List<TranscodeDevice>();
                var progressed = true;

                while (remaining > 0 && slots.Count < remaining && progressed)
                {
                    progressed = false;

                    foreach (var device in byStrength)
                    {
                        if (slots.Count >= remaining)
                        {
                            break;
                        }

                        if (freeSlots[device.Id] <= 0)
                        {
                            continue;
                        }

                        slots.Add(device);
                        freeSlots[device.Id]--;
                        progressed = true;
                    }
                }

                if (slots.Count > 0)
                {
                    // Priority first, then hardest-first; equal difficulty keeps the FIFO (id) order.
                    var candidates = queued
                        .Where(job => !used.Contains(job.Id) && job.Mode != TranscodeMode.Remux)
                        .OrderByDescending(job => job.Priority)
                        .ThenByDescending(GetDifficulty)
                        .ThenBy(job => job.Id)
                        .ToList();

                    var orderedSlots = slots
                        .OrderBy(device => strength[device.Id])
                        .ToList();

                    var strongSlots = (orderedSlots.Count + 1) / 2;

                    for (var index = 0; index < orderedSlots.Count; index++)
                    {
                        var device = orderedSlots[index];
                        var fromHardest = index < strongSlots;
                        var job = PickJob(candidates, used, device, fromHardest) ?? PickJob(candidates, used, device, !fromHardest);

                        if (job == null)
                        {
                            continue;
                        }

                        used.Add(job.Id);
                        encodeSchedule.Add((job, device, device.Id));
                    }
                }
            }

            // Remuxes stream-copy and need no encoder, so they only consume global concurrency. Merge
            // them with the encode schedule and take the highest-priority entries: scheduling every
            // remux first would starve a higher-priority encode of the global budget.
            var remuxEntries = queued
                .Where(job => job.Mode == TranscodeMode.Remux)
                .Select(job => (Job: job, Device: (TranscodeDevice)null, DeviceKey: RemuxDeviceId));

            return encodeSchedule.Concat(remuxEntries)
                .OrderByDescending(entry => entry.Job.Priority)
                .ThenBy(entry => entry.Job.Id)
                .Take(budget)
                .ToList();
        }

        private List<(TranscodeJob Job, TranscodeDevice Device, string DeviceKey)> BuildSequentialSchedule(
            List<TranscodeJob> queued,
            List<TranscodeDevice> devices,
            IDictionary<string, int> running,
            int budget)
        {
            var schedule = new List<(TranscodeJob Job, TranscodeDevice Device, string DeviceKey)>();
            var counts = new Dictionary<string, int>(running ?? new Dictionary<string, int>());
            var remaining = budget;

            foreach (var job in queued)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (job.Mode == TranscodeMode.Remux)
                {
                    // Remuxes stream-copy and need no encoder, so they only consume global concurrency.
                    schedule.Add((job, null, RemuxDeviceId));
                    remaining--;
                    continue;
                }

                var codec = ParseCodec(job.VideoCodec);
                var device = SelectDevice(devices, codec, counts, job.DeviceId, _configService.PreferHardware);

                if (device == null)
                {
                    continue;
                }

                schedule.Add((job, device, device.Id));
                counts[device.Id] = counts.TryGetValue(device.Id, out var count) ? count + 1 : 1;
                remaining--;
            }

            return schedule;
        }

        private static TranscodeJob PickJob(
            List<TranscodeJob> candidates,
            HashSet<int> used,
            TranscodeDevice device,
            bool fromHardest)
        {
            if (fromHardest)
            {
                return candidates.FirstOrDefault(job => !used.Contains(job.Id) && AllowsCodec(device, ParseCodec(job.VideoCodec)));
            }

            for (var index = candidates.Count - 1; index >= 0; index--)
            {
                var job = candidates[index];

                if (!used.Contains(job.Id) && AllowsCodec(device, ParseCodec(job.VideoCodec)))
                {
                    return job;
                }
            }

            return null;
        }

        private void HandleUnscheduledJob(TranscodeJob job, List<TranscodeDevice> devices)
        {
            if (job.Mode == TranscodeMode.Remux)
            {
                return;
            }

            var current = _repository.Find(job.Id);

            if (current == null || current.Status != TranscodeJobStatus.Queued)
            {
                return;
            }

            var codec = ParseCodec(job.VideoCodec);

            // Decide under the per-job transition gate and re-read the row, so a cancel that landed
            // after the Queued snapshot cannot be overwritten by a stale fail (the cancel and this
            // fail then serialize on the same gate).
            lock (GetTransitionLock(job.Id))
            {
                var fresh = _repository.Find(job.Id);

                if (fresh == null || fresh.Status != TranscodeJobStatus.Queued)
                {
                    return;
                }

                if (HasCapableDevice(devices, codec))
                {
                    // A capable, enabled device exists but is saturated: waiting for a slot is
                    // correct, so clear any failure timer.
                    ClearScheduleFailures(job.Id);
                }
                else if (HasPendingCapableDevice(devices, codec))
                {
                    // The snapshot is empty or every capable device is disabled or
                    // unavailable/undetected. That may be transient (a pre-probe empty snapshot), so
                    // wait for a bounded grace before failing, but do not wait forever for a device
                    // that never becomes available.
                    ClearScheduleFailure(job.Id);

                    if (RecordPendingFailure(job.Id))
                    {
                        Finish(job, TranscodeJobStatus.Failed, error: "No enabled device available");
                    }
                }
                else
                {
                    // A non-empty snapshot with no encoder for the codec at all is a genuine failure
                    // after the shorter grace.
                    ClearPendingFailure(job.Id);

                    if (RecordScheduleFailure(job.Id))
                    {
                        Finish(job, TranscodeJobStatus.Failed, error: "No enabled device supports this codec");
                    }
                }
            }
        }

        private double GetDifficulty(TranscodeJob job)
        {
            if (job.Mode == TranscodeMode.Remux)
            {
                return 0;
            }

            lock (_difficultyLock)
            {
                if (_difficultyCache.TryGetValue(job.Id, out var cached))
                {
                    return cached;
                }
            }

            var info = GetSourceMediaInfo(job);

            if (info != null && info.Width > 0 && info.Height > 0 && info.RunTime > TimeSpan.Zero)
            {
                // Total pixels to encode is the closest cheap proxy for how long the job will take.
                var work = (double)info.Width * info.Height * info.RunTime.TotalSeconds;

                lock (_difficultyLock)
                {
                    _difficultyCache[job.Id] = work;
                }

                return work;
            }

            // No probe data: fall back to the source size, adjusted for resolution when known.
            var size = job.SourceSize.GetValueOrDefault();
            var factor = info != null && info.Height > 0 ? info.Height / 1080.0 : 1.0;

            return Math.Max(1, size) * Math.Max(0.25, factor);
        }

        // Only the already-scanned library MediaInfo is used here: this runs on the scheduler pump
        // thread to estimate difficulty, and a synchronous probe of a slow/remote source would stall
        // the whole queue. The encode itself probes (SafeProbe) when it actually starts, and the
        // size-based fallback in GetDifficulty covers unscanned files.
        private MediaInfoModel GetSourceMediaInfo(TranscodeJob job)
        {
            if (job.EpisodeFileId.HasValue)
            {
                var file = _mediaFileService.Get(job.EpisodeFileId.Value);

                if (file?.MediaInfo != null)
                {
                    return file.MediaInfo;
                }
            }
            else if (job.MovieFileId.HasValue)
            {
                var file = _movieFileService.GetMovie(job.MovieFileId.Value);

                if (file?.MediaInfo != null)
                {
                    return file.MediaInfo;
                }
            }

            return null;
        }

        private void PruneDifficultyCache(List<TranscodeJob> queued)
        {
            var live = new HashSet<int>(queued.Select(job => job.Id));

            lock (_difficultyLock)
            {
                foreach (var key in _difficultyCache.Keys.Where(key => !live.Contains(key)).ToList())
                {
                    _difficultyCache.Remove(key);
                }
            }
        }

        // Skip a device that just failed an encode (driver reset, NVENC session limit, VRAM
        // exhaustion, a bad -gpu/node). The job itself still fails; the cooldown only ensures the
        // requeue is scheduled on another capable device instead of immediately repeating the failure.
        internal void RecordDeviceFailure(string deviceId)
        {
            if (deviceId.IsNullOrWhiteSpace())
            {
                return;
            }

            lock (_deviceCooldownLock)
            {
                _deviceCooldowns[deviceId] = DateTime.UtcNow;
            }
        }

        internal bool IsDeviceCooling(string deviceId)
        {
            if (deviceId.IsNullOrWhiteSpace())
            {
                return false;
            }

            lock (_deviceCooldownLock)
            {
                if (!_deviceCooldowns.TryGetValue(deviceId, out var failedAt))
                {
                    return false;
                }

                if (DateTime.UtcNow - failedAt >= DeviceCooldown)
                {
                    _deviceCooldowns.Remove(deviceId);
                    return false;
                }

                return true;
            }
        }

        private static bool HasCapableDevice(List<TranscodeDevice> devices, TranscodeCodec codec)
        {
            return (devices ?? new List<TranscodeDevice>())
                .Any(device => device.Enabled && !device.Unavailable && AllowsCodec(device, codec));
        }

        // True while a usable device might still appear: the snapshot is empty (not yet probed) or
        // some device can encode the codec but is disabled or unavailable/undetected. Only a
        // non-empty snapshot with no encoder for the codec at all is a genuine failure; this case is
        // given the longer PendingDeviceGrace before it is failed.
        private static bool HasPendingCapableDevice(List<TranscodeDevice> devices, TranscodeCodec codec)
        {
            var list = devices ?? new List<TranscodeDevice>();

            return list.Count == 0
                || list.Any(device => AllowsCodec(device, codec) && (!device.Enabled || device.Unavailable));
        }

        private bool RecordScheduleFailure(int jobId)
        {
            lock (_activeLock)
            {
                if (!_scheduleFailures.TryGetValue(jobId, out var firstSeen))
                {
                    _scheduleFailures[jobId] = DateTime.UtcNow;
                    return ScheduleFailureGrace <= TimeSpan.Zero;
                }

                return DateTime.UtcNow - firstSeen >= ScheduleFailureGrace;
            }
        }

        private void ClearScheduleFailure(int jobId)
        {
            lock (_activeLock)
            {
                _scheduleFailures.Remove(jobId);
            }
        }

        // Bounded grace for the "possibly transient" case (empty snapshot or a capable device that
        // is disabled/unavailable): unlike RecordScheduleFailure this always eventually fires.
        private bool RecordPendingFailure(int jobId)
        {
            lock (_activeLock)
            {
                if (!_pendingFailures.TryGetValue(jobId, out var firstSeen))
                {
                    _pendingFailures[jobId] = DateTime.UtcNow;
                    return PendingDeviceGrace <= TimeSpan.Zero;
                }

                return DateTime.UtcNow - firstSeen >= PendingDeviceGrace;
            }
        }

        private void ClearPendingFailure(int jobId)
        {
            lock (_activeLock)
            {
                _pendingFailures.Remove(jobId);
            }
        }

        // Clears both failure timers (the job is schedulable again, or it reached a terminal state).
        private void ClearScheduleFailures(int jobId)
        {
            lock (_activeLock)
            {
                _scheduleFailures.Remove(jobId);
                _pendingFailures.Remove(jobId);
            }
        }

        // Running jobs that no longer fit the current settings: their device was disabled/removed or
        // shrank below the number of jobs already running on it, or the global limit was lowered.
        // Oldest-started jobs are kept; the overflow (and jobs on disabled devices) are returned.
        public List<TranscodeJob> GetJobsExceedingSettings()
        {
            var maxConcurrent = Math.Max(1, _configService.MaxConcurrentJobs);
            var devices = _gpuCapabilityService.GetDevices() ?? new List<TranscodeDevice>();

            var running = _repository.GetByStatus(TranscodeJobStatus.Running)
                                     .OrderBy(job => job.StartedAt ?? DateTime.MinValue)
                                     .ThenBy(job => job.Id)
                                     .ToList();

            var blocked = new List<TranscodeJob>();

            for (var index = 0; index < running.Count; index++)
            {
                var job = running[index];

                // The global limit applies to every running job, remux included: a lowered
                // MaxConcurrentJobs must still be able to force-stop the overflow.
                if (index >= maxConcurrent)
                {
                    blocked.Add(job);
                    continue;
                }

                // A pre-probe/empty snapshot must not flag every running encode as blocked: the
                // device lookup would fail for all of them and an id-less force-stop would target
                // healthy jobs. Only a real device-disable or a limit lower flags jobs.
                if (devices.Count == 0)
                {
                    continue;
                }

                // Remux runs with device = null, so a device lookup would always "fail" and flag a
                // healthy stream-copy as blocked. Exempt it from the device check only.
                if (job.Mode == TranscodeMode.Remux || job.DeviceId == RemuxDeviceId)
                {
                    continue;
                }

                var device = devices.FirstOrDefault(candidate => candidate.Id == job.DeviceId);

                if (device == null || !device.Enabled || device.Unavailable)
                {
                    blocked.Add(job);
                    continue;
                }

                // Jobs on the same device that started before this one already consume its slots.
                var earlierOnDevice = running.Take(index)
                                             .Count(candidate => candidate.DeviceId == job.DeviceId);

                if (earlierOnDevice >= Math.Max(1, device.MaxParallel))
                {
                    blocked.Add(job);
                }
            }

            return blocked;
        }

        public List<TranscodeJob> ForceStopJobs(IEnumerable<int> jobIds)
        {
            var stopped = new List<TranscodeJob>();

            foreach (var jobId in jobIds ?? Enumerable.Empty<int>())
            {
                // Find (not Get): a stale id must be skipped, not throw and abort the whole batch.
                var job = _repository.Find(jobId);

                if (job == null)
                {
                    continue;
                }

                // Only a job whose cancel actually took effect is reported as stopped; an unknown or
                // terminal id (NotCancellable) is a no-op, not a stop.
                if (CancelJob(jobId) != CancelJobResult.Cancelled)
                {
                    continue;
                }

                stopped.Add(_repository.Find(jobId) ?? job);
            }

            return stopped;
        }

        public int ClearHistory()
        {
            var terminal = _repository.GetTerminal();

            if (terminal.Count == 0)
            {
                return 0;
            }

            // The durable rows are the convenience view, not the audit record; write a permanent
            // log line for each before deleting so the history survives in the log files.
            foreach (var job in terminal)
            {
                _logger.Info("Clearing transcode job {0} [{1}] for '{2}'{3}",
                    job.Id,
                    job.Status,
                    job.SourcePath,
                    job.Error.IsNullOrWhiteSpace() ? string.Empty : $": {job.Error}");
            }

            _repository.DeleteMany(terminal.Select(job => job.Id).ToList());

            return terminal.Count;
        }

        private long ResolveTargetBytes(TranscodeJob job, long sourceSize)
        {
            switch (job.Mode)
            {
                case TranscodeMode.TargetSize:
                    return job.TargetSize ?? 0;
                case TranscodeMode.PercentageReduction:
                    return sourceSize * (job.TargetPercent ?? 100) / 100;
                default:
                    return 0;
            }
        }

        private MediaInfoModel SafeProbe(string path)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                return _videoFileInfoReader.ProbeMediaInfo(path).MediaInfo;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to probe {0} for transcoding", path);
                return null;
            }
        }

        private void RecoverInterruptedJobs()
        {
            foreach (var job in _repository.GetByStatus(TranscodeJobStatus.Running) ?? new List<TranscodeJob>())
            {
                RunIsolated(job, RecoverRunningJob, "recover interrupted transcode job");
            }

            foreach (var job in _repository.GetByStatus(TranscodeJobStatus.Transferring) ?? new List<TranscodeJob>())
            {
                RunIsolated(job, RecoverTransferringJob, "recover interrupted transfer");
            }
        }

        // Re-runs recovery for Running/Transferring rows the startup pass missed (e.g. an IO error
        // aborted the pass, or the row was written after Handle ran). A row that still has an
        // in-memory owner, or was touched within StrandedJobGrace (so it may be mid-claim), is left
        // alone; the transfer watcher keeps LastUpdatedAt fresh, so a live copy never ages out.
        internal void ReconcileStrandedJobs()
        {
            var now = DateTime.UtcNow;

            foreach (var job in _repository.GetByStatus(TranscodeJobStatus.Running) ?? new List<TranscodeJob>())
            {
                if (IsJobOwned(job.Id) || IsWithinGrace(job, now))
                {
                    continue;
                }

                RunIsolated(job, RecoverRunningJob, "reconcile stranded transcode job");
            }

            foreach (var job in _repository.GetByStatus(TranscodeJobStatus.Transferring) ?? new List<TranscodeJob>())
            {
                if (IsJobOwned(job.Id) || IsWithinGrace(job, now))
                {
                    continue;
                }

                RunIsolated(job, RecoverTransferringJob, "reconcile stranded transfer");
            }
        }

        private bool IsJobOwned(int jobId)
        {
            lock (_activeLock)
            {
                return _active.ContainsKey(jobId) || _cancellations.ContainsKey(jobId);
            }
        }

        private bool IsWithinGrace(TranscodeJob job, DateTime now)
        {
            var lastUpdated = job.LastUpdatedAt ?? job.StartedAt;

            return lastUpdated.HasValue && now - lastUpdated.Value < StrandedJobGrace;
        }

        // One bad row (a hostile Container, an IO error, a failed persist) must never abort the whole
        // recovery pass and leave the rows after it stuck.
        private void RunIsolated(TranscodeJob job, Action<TranscodeJob> recover, string description)
        {
            try
            {
                recover(job);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to {0} {1}", description, job.Id);
            }
        }

        private void RecoverRunningJob(TranscodeJob job)
        {
            _logger.Warn("Marking interrupted transcode job {0} as failed (original untouched)", job.Id);

            job.Status = TranscodeJobStatus.Failed;
            job.Error = "Interrupted by a restart";
            job.EndedAt = DateTime.UtcNow;
            job.LastUpdatedAt = DateTime.UtcNow;

            // Only discard the working folder once the failure is durable, so a failed write cannot
            // delete the output while the row still reads Running.
            if (TryPersistRequired(job))
            {
                CleanupJobFolder(job.Id);
            }
        }

        private void RecoverTransferringJob(TranscodeJob job)
        {
            // The encode had finished; only the copy into the library was interrupted. Drop the
            // partial staging file and, if the finished output survived, return the job to review
            // so it can be resolved again without re-encoding. The isolation wrapper catches a
            // hostile destination (GetDestinationPath can throw) so the loop keeps going.
            try
            {
                DeleteQuietly(GetStagingPath(GetDestinationPath(job), job.Id));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to clean the staging file for interrupted transcode job {0}", job.Id);
            }

            if (job.OutputPath.IsNotNullOrWhiteSpace() && _diskProvider.FileExists(job.OutputPath))
            {
                _logger.Warn("Returning interrupted transfer for job {0} to review (finished output kept)", job.Id);

                job.Status = TranscodeJobStatus.AwaitingReview;
                job.Progress = 100;
                job.Error = "Transfer interrupted by a restart";
                job.LastUpdatedAt = DateTime.UtcNow;
                TryPersistRequired(job);
            }
            else
            {
                _logger.Warn("Marking interrupted transcode job {0} as failed (original untouched)", job.Id);

                job.Status = TranscodeJobStatus.Failed;
                job.Error = "Interrupted by a restart";
                job.EndedAt = DateTime.UtcNow;
                job.LastUpdatedAt = DateTime.UtcNow;

                if (TryPersistRequired(job))
                {
                    CleanupJobFolder(job.Id);
                }
            }
        }

        private void Complete(TranscodeJob job)
        {
            // Serialize the terminal write with the transfer watcher's guarded progress write (and
            // the cancel/finish transitions), so a late watcher can never lower the progress of the
            // completed row.
            lock (GetTransitionLock(job.Id))
            {
                job.Status = TranscodeJobStatus.Completed;
                job.EndedAt = DateTime.UtcNow;
                job.LastUpdatedAt = DateTime.UtcNow;

                // The completion is the durable record that the output was placed; never swallow a
                // failed write (the caller surfaces the error and the resolve can be retried).
                if (!TryPersistRequired(job))
                {
                    throw new InvalidOperationException($"Unable to persist the completion of transcode job {job.Id}");
                }

                Publish(job);
            }

            ClearScheduleFailures(job.Id);
            CleanupJobFolder(job.Id);
        }

        private void UpdateLibraryFile(TranscodeJob job, string path)
        {
            try
            {
                var mediaInfo = _videoFileInfoReader.GetMediaInfo(path);
                var size = _diskProvider.GetFileSize(path);

                if (job.MediaType == MediaType.Series && job.EpisodeFileId.HasValue)
                {
                    var file = _mediaFileService.Get(job.EpisodeFileId.Value);

                    if (file != null)
                    {
                        file.Size = size;
                        file.MediaInfo = mediaInfo;
                        file.OriginalFilePath = path;
                        file.RelativePath = GetUpdatedRelativePath(file.RelativePath, path);
                        _mediaFileService.Update(file);
                    }
                }
                else if (job.MediaType == MediaType.Movie && job.MovieFileId.HasValue)
                {
                    var file = _movieFileService.GetMovie(job.MovieFileId.Value);

                    if (file != null)
                    {
                        file.Size = size;
                        file.MediaInfo = mediaInfo;
                        file.OriginalFilePath = path;
                        file.RelativePath = GetUpdatedRelativePath(file.RelativePath, path);
                        _movieFileService.Update(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to refresh library metadata after resolving transcode job {0}", job.Id);
            }
        }

        // Keeps the directory of the existing library-relative path but adopts the new (possibly
        // tagged/container-changed) file name.
        private static string GetUpdatedRelativePath(string currentRelativePath, string absolutePath)
        {
            var directory = Path.GetDirectoryName(currentRelativePath);

            if (directory.IsNullOrWhiteSpace())
            {
                return Path.GetFileName(absolutePath);
            }

            return Path.Combine(directory, Path.GetFileName(absolutePath));
        }

        private static string ApplyTag(string name, string tag)
        {
            if (name.IsNullOrWhiteSpace() || tag.IsNullOrWhiteSpace())
            {
                return name;
            }

            var suffix = $" [{tag}]";

            while (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - suffix.Length).TrimEnd();
            }

            return name + suffix;
        }

        private static string GetKeepBothPath(string path)
        {
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var candidate = Path.Combine(directory, name + ".orig" + extension);
            var index = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, $"{name}.orig.{index}{extension}");
                index++;
            }

            return candidate;
        }

        private void CleanupJobFolder(int jobId)
        {
            try
            {
                var folder = Path.Combine(GetTempFolder(), jobId.ToString());

                if (_diskProvider.FolderExists(folder))
                {
                    _diskProvider.DeleteFolder(folder, true);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to clean up the working folder for transcode job {0}", jobId);
            }
        }

        private void DeleteQuietly(string path)
        {
            try
            {
                if (path.IsNotNullOrWhiteSpace() && _diskProvider.FileExists(path))
                {
                    _diskProvider.DeleteFile(path);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to delete {0}", path);
            }
        }

        private void Finish(TranscodeJob job, TranscodeJobStatus status, string error = null, string message = null)
        {
            // The terminal write is a status transition too: serialize it with the claim/cancel
            // paths so a concurrent cancel and this finish cannot both persist.
            lock (GetTransitionLock(job.Id))
            {
                var persisted = _repository.Find(job.Id);

                // Never overwrite a row that already reached a terminal state: a cancel that won
                // the race against a scheduler fail (or a duplicate finish) must keep its result.
                // Legitimate Queued/Running -> Failed/Cancelled/Skipped writes still proceed.
                if (persisted != null && IsTerminalStatus(persisted.Status))
                {
                    ClearScheduleFailures(job.Id);
                    return;
                }

                job.Status = status;
                job.Error = error;
                job.Message = message;
                job.EndedAt = DateTime.UtcNow;
                job.LastUpdatedAt = DateTime.UtcNow;

                if (status == TranscodeJobStatus.Cancelled ||
                    status == TranscodeJobStatus.Failed ||
                    status == TranscodeJobStatus.Skipped)
                {
                    // The encode's live progress fields describe work that no longer exists, and the
                    // working folder (with its output) is discarded below, so clear both rather than
                    // showing e.g. "Cancelled 42% / 12x / 3.1 MB/s". The transfer-cancel path keeps
                    // its output by returning to AwaitingReview, not through Finish.
                    job.Speed = null;
                    job.Fps = null;
                    job.Eta = null;
                    job.OutputPath = null;
                    job.OutputSize = null;
                }

                // The terminal write is state-defining, so a failed persist must surface instead of
                // silently leaving the row in its previous state.
                if (!TryPersistRequired(job))
                {
                    throw new InvalidOperationException($"Unable to persist the {status} state of transcode job {job.Id}");
                }

                Publish(job);
            }

            ClearScheduleFailures(job.Id);

            // Failed/Cancelled/Skipped discard the working output, so the two-pass logs and partials
            // must not leak. Completed is cleaned by Complete after the output is placed.
            if (status != TranscodeJobStatus.Completed)
            {
                CleanupJobFolder(job.Id);
            }
        }

        private static bool IsTerminalStatus(TranscodeJobStatus status)
        {
            return status == TranscodeJobStatus.Completed ||
                   status == TranscodeJobStatus.Failed ||
                   status == TranscodeJobStatus.Cancelled ||
                   status == TranscodeJobStatus.Skipped;
        }

        private void Persist(TranscodeJob job)
        {
            try
            {
                _repository.Update(job);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to persist the {0} status of transcode job {1}", job.Status, job.Id);
            }
        }

        // Required variant of Persist for writes that define mutual exclusion / durability: unlike
        // Persist it never swallows a repository failure, so the caller can abort (e.g. never start a
        // second transfer) instead of silently continuing on a stale row. Returns false on failure.
        private bool TryPersistRequired(TranscodeJob job)
        {
            try
            {
                _repository.Update(job);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to persist the {0} status of transcode job {1}", job.Status, job.Id);
                return false;
            }
        }

        private void Publish(TranscodeJob job)
        {
            try
            {
                _eventAggregator.PublishEvent(new TranscodeProgressEvent
                {
                    JobId = job.Id,
                    MediaType = job.MediaType,
                    Status = job.Status,
                    Progress = job.Progress,
                    Speed = job.Speed,
                    Fps = job.Fps,
                    Eta = job.Eta,
                    DeviceId = job.DeviceId,
                    SizeBefore = job.SourceSize,
                    SizeAfter = job.OutputSize,
                    Message = job.Message
                });
            }
            catch (Exception ex)
            {
                // A subscriber must never be able to leave a row set to Running with no in-memory
                // token (the Task.Run catch only logs, so the abort would otherwise be swallowed).
                _logger.Error(ex, "Unable to publish the {0} status of transcode job {1}", job.Status, job.Id);
            }
        }

        private string GetOutputPath(TranscodeJob job)
        {
            var fileName = job.SourcePath == null ? $"{job.Id}.tmp" : Path.GetFileName(job.SourcePath);

            if (job.Container.IsNotNullOrWhiteSpace())
            {
                fileName = Path.ChangeExtension(fileName, "." + job.Container.TrimStart('.'));
            }

            var folder = Path.Combine(GetTempFolder(), job.Id.ToString());
            var path = Path.Combine(folder, fileName);

            if (!IsInsideDirectory(folder, path))
            {
                throw new InvalidOperationException($"The transcoded output path '{path}' escapes the working folder");
            }

            return path;
        }

        // Guards against a hostile job Container (or Tag) turning a filename into a traversal that
        // Path.Combine would otherwise happily follow out of the intended directory.
        internal static bool IsInsideDirectory(string directory, string candidate)
        {
            if (candidate.IsNullOrWhiteSpace())
            {
                return false;
            }

            var normalizedDirectory = Path.GetFullPath(directory.IsNullOrWhiteSpace() ? "." : directory);
            var normalizedCandidate = Path.GetFullPath(candidate);

            // Windows paths are case-insensitive, so a case-differing path is still inside; POSIX
            // filesystems keep the case-sensitive Ordinal comparison.
            var prefix = normalizedDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), PathComparison)
                ? normalizedDirectory
                : normalizedDirectory + Path.DirectorySeparatorChar;

            return normalizedCandidate.StartsWith(prefix, PathComparison);
        }

        private static string GetDestinationPath(TranscodeJob job)
        {
            if (job.SourcePath.IsNullOrWhiteSpace() || (job.Container.IsNullOrWhiteSpace() && job.Tag.IsNullOrWhiteSpace()))
            {
                return job.SourcePath;
            }

            var directory = Path.GetDirectoryName(job.SourcePath) ?? string.Empty;
            var extension = job.Container.IsNotNullOrWhiteSpace()
                ? "." + job.Container.TrimStart('.')
                : Path.GetExtension(job.SourcePath);
            var name = Path.GetFileNameWithoutExtension(job.SourcePath);

            if (job.Tag.IsNotNullOrWhiteSpace())
            {
                name = ApplyTag(name, job.Tag);
            }

            var destination = Path.Combine(directory, name + extension);

            if (!IsInsideDirectory(directory, destination))
            {
                throw new InvalidOperationException($"The transcode destination path '{destination}' escapes the source folder");
            }

            return destination;
        }
    }
}
