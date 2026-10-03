using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeServiceFixture : CoreTest<TranscodeService>
    {
        private TranscodeJob _job;
        private TranscodeDevice _device;

        [SetUp]
        public void Setup()
        {
            _job = new TranscodeJob
            {
                Id = 1,
                SourcePath = @"C:\media\episode.mkv".AsOsAgnostic(),
                SourceSize = 8_000_000,
                Mode = TranscodeMode.TargetSize,
                TargetSize = 4_000_000,
                VideoCodec = "hevc",
                Preset = "medium",
                Status = TranscodeJobStatus.Queued
            };

            _device = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Name = "CPU",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 100,
                Capabilities = new DeviceCapability
                {
                    Id = "software",
                    Kind = TranscodeDeviceKind.Software,
                    Supported = true,
                    Encoders = new List<string> { "libx264", "libx265", "libsvtav1" }
                }
            };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(service => service.MediaCompressionEnabled)
                  .Returns(true);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(service => service.TranscodeTempFolder)
                  .Returns(string.Empty);

            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(info => info.AppDataFolder)
                  .Returns(@"C:\appdata".AsOsAgnostic());

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Get(1))
                  .Returns(_job);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(1))
                  .Returns(_job);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(long.MaxValue);

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(reader => reader.ProbeMediaInfo(It.IsAny<string>()))
                  .Returns(new MediaInfoProbeResult
                  {
                      MediaInfo = new MediaInfoModel
                      {
                          RunTime = TimeSpan.FromSeconds(60),
                          VideoBitrate = 5_000_000,
                          AudioStreams = new List<MediaInfoAudioStreamModel> { new MediaInfoAudioStreamModel { Bitrate = 128_000 } }
                      }
                  });

            Mocker.GetMock<ITranscodeArgumentBuilder>()
                  .Setup(builder => builder.Build(It.IsAny<TranscodeJob>(), It.IsAny<TranscodeDevice>(), It.IsAny<TranscodeCodec>(), It.IsAny<long>(), It.IsAny<double>(), It.IsAny<MediaInfoModel>(), It.IsAny<string>()))
                  .Returns(() =>
                  {
                      var plan = new TranscodePlan();

                      plan.Commands.Add("-i input -f null -");

                      return plan;
                  });
        }

        [Test]
        public void derive_video_bitrate_should_subtract_audio_and_overhead()
        {
            var bitrate = TranscodeService.DeriveVideoBitrate(1000, 10, 100);

            // 1000 bytes * 8 / 10s = 800bps total, -5% overhead (40) -100 audio = 660
            bitrate.Should().Be(660);
        }

        [Test]
        public void derive_video_bitrate_should_return_zero_for_invalid_input()
        {
            TranscodeService.DeriveVideoBitrate(0, 10, 0).Should().Be(0);
            TranscodeService.DeriveVideoBitrate(1000, 0, 0).Should().Be(0);
        }

        [Test]
        public void should_skip_when_target_bitrate_is_not_smaller()
        {
            TranscodeService.ShouldSkip(1000, 1200).Should().BeTrue();
            TranscodeService.ShouldSkip(1000, 1000).Should().BeTrue();
            TranscodeService.ShouldSkip(1000, 500).Should().BeFalse();
        }

        [Test]
        public void has_enough_free_space_should_require_source_plus_margin()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(1_000_000);

            Subject.HasEnoughFreeSpace(1_000_000).Should().BeFalse();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(1_100_000);

            Subject.HasEnoughFreeSpace(1_000_000).Should().BeTrue();
        }

        [Test]
        public void execute_should_fail_the_job_when_the_runner_fails()
        {
            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(new TranscodeResult { Success = false, Error = "boom" });

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Be("boom");
        }

        [Test]
        public void execute_should_skip_when_target_bitrate_is_not_smaller_than_source()
        {
            _job.TargetSize = 100_000_000;

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Skipped);
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_skip_a_remux_when_the_container_already_matches()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = "mkv";
            _job.SourcePath = @"C:\media\episode.mkv".AsOsAgnostic();

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Skipped);
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_fail_when_there_is_not_enough_free_space()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(0);

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("free space");
        }

        [Test]
        public void execute_should_move_to_awaiting_review_on_success()
        {
            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(new TranscodeResult { Success = true, OutputPath = "/tmp/out.mkv", OutputSize = 123 });

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            _job.OutputSize.Should().Be(123);
        }

        private void GivenReviewableJob()
        {
            _job.Status = TranscodeJobStatus.AwaitingReview;
            _job.MediaType = MediaType.Series;
            _job.EpisodeFileId = 10;
            _job.OutputPath = @"C:\appdata\transcode\1\out.mkv".AsOsAgnostic();
            _job.OutputSize = 500;

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FileExists(_job.OutputPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(_job.OutputPath))
                  .Returns(500);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(_job.SourcePath))
                  .Returns(500);

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(reader => reader.GetMediaInfo(_job.SourcePath))
                  .Returns(new MediaInfoModel());

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(10))
                  .Returns(new EpisodeFile { Id = 10, RelativePath = "episode.mkv", Size = 1_000_000 });
        }

        [Test]
        public void resolve_overwrite_should_recycle_original_and_move_output_into_place()
        {
            GivenReviewableJob();

            var job = Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            job.Status.Should().Be(TranscodeJobStatus.Completed);

            // The original is moved to a sibling first and recycled only after the replacement is in
            // place, so a failed rename can never lose it.
            var original = _job.SourcePath + ".transcode-original~";

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(bin => bin.DeleteFile(original, It.IsAny<string>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.SourcePath, original, TransferMode.Move, true), Times.Once);

            // The finished output is copied to a staging file first and only then renamed into place,
            // so the original is never removed before the replacement is verified.
            var staging = _job.SourcePath + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(staging, _job.SourcePath, TransferMode.Move, true), Times.Once);

            job.Progress.Should().Be(100);
        }

        [Test]
        public void resolve_overwrite_should_restore_the_original_when_the_final_move_fails()
        {
            GivenReviewableJob();

            var original = _job.SourcePath + ".transcode-original~";

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, It.IsAny<string>(), TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Returns(TransferMode.Copy);

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.SourcePath + ".1.transcode~", _job.SourcePath, TransferMode.Move, true))
                  .Throws(new IOException("rename failed"));

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FileExists(original))
                  .Returns(true);

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<IOException>();

            // The original that was moved aside is restored; the failure keeps the job in review.
            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(original, _job.SourcePath, TransferMode.Move, true), Times.Once);

            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            _job.Error.Should().Contain("rename failed");
        }

        [Test]
        public void resolve_should_return_to_review_when_the_transfer_fails()
        {
            GivenReviewableJob();

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, It.IsAny<string>(), TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Throws(new IOException("No space left on device"));

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<IOException>();

            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            _job.Error.Should().Contain("No space left");
            _job.Progress.Should().Be(100);

            // The original is not removed until the replacement copy has succeeded.
            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(bin => bin.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void resolve_should_refuse_a_full_destination_without_touching_the_original()
        {
            GivenReviewableJob();

            var outputMount = new Mock<IMount>();
            outputMount.SetupGet(mount => mount.RootDirectory).Returns("/transcode");

            var libraryMount = new Mock<IMount>();
            libraryMount.SetupGet(mount => mount.RootDirectory).Returns("/library");

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetMount(_job.OutputPath))
                  .Returns(outputMount.Object);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetMount(_job.SourcePath))
                  .Returns(libraryMount.Object);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(0);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(_job.OutputPath))
                  .Returns(500);

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<InvalidOperationException>();
            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            _job.Error.Should().Contain("free space");

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(bin => bin.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void resolve_should_publish_a_transferring_status_before_completing()
        {
            GivenReviewableJob();

            var statuses = new List<TranscodeJobStatus>();

            Mocker.GetMock<IEventAggregator>()
                  .Setup(aggregator => aggregator.PublishEvent(It.IsAny<TranscodeProgressEvent>()))
                  .Callback<TranscodeProgressEvent>(message => statuses.Add(message.Status));

            Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            statuses.Should().Contain(TranscodeJobStatus.Transferring);
            statuses.Should().Contain(TranscodeJobStatus.Completed);
        }

        [Test]
        public void resolve_jobs_should_claim_the_whole_batch_before_transferring()
        {
            GivenReviewableJob();

            var second = new TranscodeJob
            {
                Id = 2,
                MediaType = MediaType.Series,
                EpisodeFileId = 11,
                SourcePath = @"C:\media\episode2.mkv".AsOsAgnostic(),
                OutputPath = @"C:\appdata\transcode\2\out.mkv".AsOsAgnostic(),
                SourceSize = 8_000_000,
                Status = TranscodeJobStatus.AwaitingReview
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Get(2))
                  .Returns(second);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FileExists(second.OutputPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(second.OutputPath))
                  .Returns(500);

            var secondStatusAtFirstTransfer = new List<TranscodeJobStatus>();

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, It.IsAny<string>(), TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Callback(() => secondStatusAtFirstTransfer.Add(second.Status));

            var resolved = Subject.ResolveJobs(new[] { 1, 2 }, TranscodeReviewAction.Overwrite);

            resolved.Should().HaveCount(2);
            resolved.Should().OnlyContain(result => result.Success);

            // The second job is already claimed (Transferring) while the first is still being copied,
            // so a refresh mid-batch cannot offer it as reviewable again.
            secondStatusAtFirstTransfer.Should().Equal(TranscodeJobStatus.Transferring);
        }

        [Test]
        public void cancel_should_abort_a_transferring_job_and_keep_the_output_for_review()
        {
            GivenReviewableJob();

            using var transferStarted = new ManualResetEventSlim(false);

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, It.IsAny<string>(), TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Returns((string source, string target, TransferMode mode, bool overwrite, CancellationToken token) =>
                  {
                      transferStarted.Set();
                      token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                      token.ThrowIfCancellationRequested();

                      return TransferMode.Copy;
                  });

            var resolve = Task.Run(() => Subject.Resolve(1, TranscodeReviewAction.Overwrite));

            transferStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            Subject.CancelJob(1);

            // The abort returns the job to review with the finished output kept, and the in-flight
            // resolve observes the cancellation instead of finalizing the (partial) copy.
            SpinWait.SpinUntil(() => _job.Status == TranscodeJobStatus.AwaitingReview, TimeSpan.FromSeconds(5)).Should().BeTrue();
            _job.Error.Should().Contain("cancelled");

            SpinWait.SpinUntil(() => resolve.IsCompleted, TimeSpan.FromSeconds(5)).Should().BeTrue();
            resolve.IsFaulted.Should().BeTrue();
        }

        [Test]
        public void resolve_overwrite_should_switch_the_container_and_relative_path()
        {
            GivenReviewableJob();

            _job.Container = "mkv";
            _job.SourcePath = @"C:\media\episode.mp4".AsOsAgnostic();

            var episodeFile = new EpisodeFile { Id = 10, RelativePath = "episode.mp4", Size = 1_000_000 };

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(10))
                  .Returns(episodeFile);

            Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            var destination = @"C:\media\episode.mkv".AsOsAgnostic();
            var staging = destination + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(staging, destination, TransferMode.Move, true), Times.Once);

            episodeFile.RelativePath.Should().Be("episode.mkv");
        }

        [Test]
        public void resolve_overwrite_should_append_the_tag_and_update_the_relative_path()
        {
            GivenReviewableJob();

            _job.Tag = "Transcoded";
            _job.SourcePath = @"C:\media\episode.mkv".AsOsAgnostic();

            var episodeFile = new EpisodeFile
            {
                Id = 10,
                RelativePath = @"Season 1\episode.mkv".AsOsAgnostic(),
                Size = 1_000_000
            };

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(10))
                  .Returns(episodeFile);

            Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            var destination = @"C:\media\episode [Transcoded].mkv".AsOsAgnostic();
            var staging = destination + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(staging, destination, TransferMode.Move, true), Times.Once);

            episodeFile.RelativePath.Should().Be(@"Season 1\episode [Transcoded].mkv".AsOsAgnostic());
        }

        [Test]
        public void resolve_keep_both_should_leave_the_original_when_the_output_is_tagged()
        {
            GivenReviewableJob();

            _job.Tag = "Transcoded";
            _job.SourcePath = @"C:\media\episode.mkv".AsOsAgnostic();

            Subject.Resolve(1, TranscodeReviewAction.KeepBoth);

            var destination = @"C:\media\episode [Transcoded].mkv".AsOsAgnostic();
            var staging = destination + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.SourcePath, It.IsAny<string>(), TransferMode.Move, false), Times.Never);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(staging, destination, TransferMode.Move, true), Times.Once);

            _job.OriginalPath.Should().Be(_job.SourcePath);
        }

        [Test]
        public void resolve_keep_both_should_preserve_original_as_orig()
        {
            GivenReviewableJob();

            var job = Subject.Resolve(1, TranscodeReviewAction.KeepBoth);

            job.Status.Should().Be(TranscodeJobStatus.Completed);
            job.OriginalPath.Should().Contain(".orig");

            var staging = _job.SourcePath + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.SourcePath, It.Is<string>(path => path.Contains(".orig")), TransferMode.Move, false), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(staging, _job.SourcePath, TransferMode.Move, true), Times.Once);
        }

        [Test]
        public void resolve_discard_should_delete_output_without_touching_original()
        {
            GivenReviewableJob();

            var job = Subject.Resolve(1, TranscodeReviewAction.Discard);

            job.Status.Should().Be(TranscodeJobStatus.Completed);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.DeleteFile(_job.OutputPath), Times.Once);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void select_device_should_respect_priority_and_capacity()
        {
            var devices = new List<TranscodeDevice>
            {
                _device,
                new TranscodeDevice
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Name = "GPU 0",
                    Supported = true,
                    Enabled = true,
                    MaxParallel = 1,
                    Priority = 10,
                    Capabilities = new DeviceCapability
                    {
                        Id = "nvenc:0",
                        Kind = TranscodeDeviceKind.Nvidia,
                        Supported = true,
                        Encoders = new List<string> { "hevc_nvenc" }
                    }
                }
            };

            // Preferred hardware device is chosen when it has capacity.
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, new Dictionary<string, int>(), null).Id.Should().Be("nvenc:0");

            // Saturated hardware falls back to the next capable device.
            var running = new Dictionary<string, int> { { "nvenc:0", 1 } };
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, running, null).Id.Should().Be("software");

            // A device that cannot encode the requested codec is never selected.
            TranscodeService.SelectDevice(devices, TranscodeCodec.Av1, running, null).Id.Should().Be("software");
        }

        private void GivenRunning(params TranscodeJob[] jobs)
        {
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running))
                  .Returns(jobs.ToList());
        }

        private static TranscodeDevice GivenDevice(bool enabled, int maxParallel)
        {
            return new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Name = "CPU",
                Supported = true,
                Enabled = enabled,
                MaxParallel = maxParallel,
                Priority = 100
            };
        }

        [Test]
        public void blocked_jobs_should_include_a_job_on_a_disabled_device()
        {
            GivenRunning(new TranscodeJob { Id = 1, DeviceId = "software", StartedAt = DateTime.UtcNow });

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(2);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { GivenDevice(false, 1) });

            Subject.GetJobsExceedingSettings().Select(job => job.Id).Should().Equal(1);
        }

        [Test]
        public void blocked_jobs_should_keep_the_oldest_and_overflow_a_shrunk_device()
        {
            var first = new TranscodeJob { Id = 1, DeviceId = "software", StartedAt = DateTime.UtcNow };
            var second = new TranscodeJob { Id = 2, DeviceId = "software", StartedAt = DateTime.UtcNow.AddSeconds(1) };

            GivenRunning(first, second);

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(5);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { GivenDevice(true, 1) });

            Subject.GetJobsExceedingSettings().Select(job => job.Id).Should().Equal(2);
        }

        [Test]
        public void blocked_jobs_should_include_the_global_overflow()
        {
            var first = new TranscodeJob { Id = 1, DeviceId = "software", StartedAt = DateTime.UtcNow };
            var second = new TranscodeJob { Id = 2, DeviceId = "software", StartedAt = DateTime.UtcNow.AddSeconds(1) };
            var third = new TranscodeJob { Id = 3, DeviceId = "software", StartedAt = DateTime.UtcNow.AddSeconds(2) };

            GivenRunning(first, second, third);

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(2);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { GivenDevice(true, 10) });

            Subject.GetJobsExceedingSettings().Select(job => job.Id).Should().Equal(3);
        }

        [Test]
        public void blocked_jobs_should_ignore_an_empty_device_snapshot()
        {
            GivenRunning(new TranscodeJob { Id = 1, DeviceId = "software", StartedAt = DateTime.UtcNow });

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(2);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice>());

            // A pre-probe/empty snapshot must not flag every running encode as blocked.
            Subject.GetJobsExceedingSettings().Should().BeEmpty();
        }

        [Test]
        public void force_stop_jobs_should_cancel_a_queued_job()
        {
            var queued = new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Queued };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Get(5)).Returns(queued);
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(5)).Returns(queued);

            var stopped = Subject.ForceStopJobs(new[] { 5 });

            queued.Status.Should().Be(TranscodeJobStatus.Cancelled);
            stopped.Single().Status.Should().Be(TranscodeJobStatus.Cancelled);
        }

        [Test]
        public void force_stop_jobs_should_skip_missing_ids_and_still_cancel_existing_ones()
        {
            var queued = new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Queued };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(5)).Returns(queued);
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(999)).Returns((TranscodeJob)null);

            // The real repository's Get throws for a missing id; Find returns null.
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Get(999))
                  .Throws(new ModelNotFoundException(typeof(TranscodeJob), 999));

            // A stale id must not throw (Get would) or abort the ids that follow it.
            var stopped = Subject.ForceStopJobs(new[] { 999, 5 });

            queued.Status.Should().Be(TranscodeJobStatus.Cancelled);
            stopped.Select(job => job.Id).Should().Equal(5);
        }

        [Test]
        public void clearing_history_should_delete_terminal_jobs_and_return_the_count()
        {
            var completed = new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Completed, SourcePath = "/media/a.mkv" };
            var failed = new TranscodeJob { Id = 2, Status = TranscodeJobStatus.Failed, SourcePath = "/media/b.mkv", Error = "boom" };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetTerminal())
                  .Returns(new List<TranscodeJob> { completed, failed });

            var cleared = Subject.ClearHistory();

            cleared.Should().Be(2);
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Verify(
                      repository => repository.DeleteMany(It.Is<IEnumerable<int>>(ids => ids.Count() == 2 && ids.Contains(1) && ids.Contains(2))),
                      Times.Once);
        }

        [Test]
        public void clearing_history_should_do_nothing_when_there_are_no_terminal_jobs()
        {
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetTerminal())
                  .Returns(new List<TranscodeJob>());

            Subject.ClearHistory().Should().Be(0);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Verify(repository => repository.DeleteMany(It.IsAny<IEnumerable<int>>()), Times.Never);
        }

        [Test]
        public void select_device_should_honour_per_codec_toggles()
        {
            var device = new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia,
                Name = "GPU 0",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 10,
                Capabilities = new DeviceCapability
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Supported = true,
                    Encoders = new List<string> { "hevc_nvenc" }
                },
                Codecs = new List<TranscodeCodecSetting>
                {
                    new TranscodeCodecSetting { Codec = "hevc", Encoder = "hevc_nvenc", Supported = true, Enabled = false },
                    new TranscodeCodecSetting { Codec = "av1", Encoder = "av1_nvenc", Supported = false, Enabled = true }
                }
            };

            var devices = new List<TranscodeDevice> { device };
            var running = new Dictionary<string, int>();

            // A supported codec the user turned off is not selected.
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, running, null).Should().BeNull();

            // An unsupported codec the user forced on is selected and falls back to the conventional encoder.
            var selected = TranscodeService.SelectDevice(devices, TranscodeCodec.Av1, running, null);
            selected.Should().NotBeNull();
            TranscodeArgumentBuilder.GetEncoderFor(selected, TranscodeCodec.Av1, allowUnsupported: true).Should().Be("av1_nvenc");
        }

        [Test]
        public void select_device_should_prefer_hardware_when_requested()
        {
            var software = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Name = "CPU",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 100,
                Capabilities = new DeviceCapability
                {
                    Id = "software",
                    Kind = TranscodeDeviceKind.Software,
                    Supported = true,
                    Encoders = new List<string> { "libx265" }
                }
            };

            var hardware = new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia,
                Name = "GPU 0",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 500,
                Capabilities = new DeviceCapability
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Supported = true,
                    Encoders = new List<string> { "hevc_nvenc" }
                }
            };

            var devices = new List<TranscodeDevice> { software, hardware };
            var running = new Dictionary<string, int>();

            // Priority alone would pick software; PreferHardware flips this to the GPU.
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, running, null, preferHardware: false).Id.Should().Be("software");
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, running, null, preferHardware: true).Id.Should().Be("nvenc:0");

            // A saturated GPU falls back to software even with PreferHardware on.
            running["nvenc:0"] = 1;
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, running, null, preferHardware: true).Id.Should().Be("software");
        }

        [Test]
        public void select_device_should_fill_the_preferred_device_before_falling_back()
        {
            var software = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Name = "CPU",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 100,
                Capabilities = new DeviceCapability
                {
                    Id = "software",
                    Kind = TranscodeDeviceKind.Software,
                    Supported = true,
                    Encoders = new List<string> { "libx265" }
                }
            };

            var hardware = new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia,
                Name = "GPU 0",
                Supported = true,
                Enabled = true,
                MaxParallel = 2,
                Priority = 10,
                Capabilities = new DeviceCapability
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Supported = true,
                    Encoders = new List<string> { "hevc_nvenc" }
                }
            };

            var devices = new List<TranscodeDevice> { software, hardware };

            // Hardware preference fills the GPU up to its Max parallel first, even while the CPU is
            // idle; the CPU is only used once the GPU is saturated.
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, new Dictionary<string, int>(), null, preferHardware: true).Id.Should().Be("nvenc:0");
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, new Dictionary<string, int> { { "nvenc:0", 1 } }, null, preferHardware: true).Id.Should().Be("nvenc:0");
            TranscodeService.SelectDevice(devices, TranscodeCodec.Hevc, new Dictionary<string, int> { { "nvenc:0", 2 } }, null, preferHardware: true).Id.Should().Be("software");
        }

        private static TranscodeDevice GivenNvidiaDevice(int maxParallel, int priority = 10)
        {
            return new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia,
                Name = "GPU 0",
                Supported = true,
                Enabled = true,
                MaxParallel = maxParallel,
                Priority = priority,
                Capabilities = new DeviceCapability
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Supported = true,
                    Encoders = new List<string> { "hevc_nvenc" }
                }
            };
        }

        private static TranscodeJob GivenEncodeJob(int id, long sourceSize)
        {
            return new TranscodeJob
            {
                Id = id,
                MediaType = MediaType.Series,
                SourcePath = $"/media/job{id}.mkv".AsOsAgnostic(),
                OutputPath = $"/appdata/transcode/{id}/out.mkv".AsOsAgnostic(),
                SourceSize = sourceSize,
                VideoCodec = "hevc",
                Mode = TranscodeMode.Quality,
                Status = TranscodeJobStatus.Queued
            };
        }

        [Test]
        public void build_schedule_should_pair_the_easiest_jobs_with_the_weakest_device()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 2)
            };

            var jobs = new List<TranscodeJob>
            {
                GivenEncodeJob(1, 10_000),
                GivenEncodeJob(2, 5_000),
                GivenEncodeJob(3, 1_000)
            };

            var schedule = Subject.BuildSchedule(jobs, devices, new Dictionary<string, int>(), budget: 2);

            // Two slots spread across the two devices: the GPU takes the hardest, the CPU the easiest.
            schedule.Should().HaveCount(2);
            schedule.Single(entry => entry.Device.Id == "nvenc:0").Job.Id.Should().Be(1);
            schedule.Single(entry => entry.Device.Id == "software").Job.Id.Should().Be(3);
        }

        [Test]
        public void build_schedule_should_fill_a_second_slot_on_the_strongest_device_with_the_next_hardest()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 2)
            };

            var jobs = new List<TranscodeJob>
            {
                GivenEncodeJob(1, 10_000),
                GivenEncodeJob(2, 5_000),
                GivenEncodeJob(3, 1_000)
            };

            var schedule = Subject.BuildSchedule(jobs, devices, new Dictionary<string, int>(), budget: 3);

            schedule.Should().HaveCount(3);
            schedule.Where(entry => entry.Device.Id == "nvenc:0").Select(entry => entry.Job.Id).Should().Equal(1, 2);
            schedule.Single(entry => entry.Device.Id == "software").Job.Id.Should().Be(3);
        }

        [Test]
        public void build_schedule_should_use_every_idle_device_before_packing_one()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 2)
            };

            var jobs = new List<TranscodeJob>
            {
                GivenEncodeJob(1, 3_000),
                GivenEncodeJob(2, 2_000),
                GivenEncodeJob(3, 1_000)
            };

            var schedule = Subject.BuildSchedule(jobs, devices, new Dictionary<string, int>(), budget: 2);

            schedule.Select(entry => entry.Device.Id).Should().BeEquivalentTo(new[] { "nvenc:0", "software" });
        }

        [Test]
        public void build_schedule_should_honour_a_pinned_device()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 1)
            };

            var pinned = GivenEncodeJob(1, 10_000);
            pinned.DeviceId = "software";

            var schedule = Subject.BuildSchedule(new List<TranscodeJob> { pinned }, devices, new Dictionary<string, int>(), budget: 1);

            schedule.Should().HaveCount(1);
            schedule[0].Device.Id.Should().Be("software");
            schedule[0].Job.Id.Should().Be(1);
        }

        [Test]
        public void build_schedule_should_run_a_remux_without_a_device_slot()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 1)
            };

            var remux = GivenEncodeJob(1, 10_000);
            remux.Mode = TranscodeMode.Remux;
            remux.VideoCodec = null;

            var schedule = Subject.BuildSchedule(
                new List<TranscodeJob> { remux, GivenEncodeJob(2, 5_000) },
                devices,
                new Dictionary<string, int>(),
                budget: 2);

            schedule.Should().HaveCount(2);
            var remuxEntry = schedule.Single(entry => entry.Job.Id == 1);
            remuxEntry.Device.Should().BeNull();
            remuxEntry.DeviceKey.Should().Be(TranscodeService.RemuxDeviceId);

            // The remux does not consume a device slot, so the encode still gets one.
            schedule.Single(entry => entry.Job.Id == 2).Device.Should().NotBeNull();
        }

        [Test]
        public void build_schedule_should_not_let_a_low_priority_remux_starve_a_high_priority_encode()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" })
            };

            var remux = GivenEncodeJob(1, 10_000);
            remux.Mode = TranscodeMode.Remux;
            remux.VideoCodec = null;
            remux.Priority = 1;

            var encode = GivenEncodeJob(2, 5_000);
            encode.Priority = 10;

            var schedule = Subject.BuildSchedule(
                new List<TranscodeJob> { remux, encode },
                devices,
                new Dictionary<string, int>(),
                budget: 1);

            // The single global slot goes to the higher-priority encode, not the remux.
            schedule.Should().HaveCount(1);
            schedule[0].Job.Id.Should().Be(2);
        }

        [Test]
        public void build_schedule_should_run_a_higher_priority_remux_before_a_lower_priority_encode()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeEasiestJobsFirst).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" })
            };

            var remux = GivenEncodeJob(1, 10_000);
            remux.Mode = TranscodeMode.Remux;
            remux.VideoCodec = null;
            remux.Priority = 10;

            var encode = GivenEncodeJob(2, 5_000);
            encode.Priority = 1;

            var schedule = Subject.BuildSchedule(
                new List<TranscodeJob> { remux, encode },
                devices,
                new Dictionary<string, int>(),
                budget: 1);

            schedule.Should().HaveCount(1);
            schedule[0].Job.Id.Should().Be(1);
        }

        [Test]
        public void build_schedule_should_process_the_queue_in_order_and_fill_the_preferred_device()
        {
            // Default setting (TranscodeEasiestJobsFirst off): sequential.
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 2)
            };

            // Deliberately out of size order: sequential mode must keep the queue order.
            var jobs = new List<TranscodeJob>
            {
                GivenEncodeJob(1, 1_000),
                GivenEncodeJob(2, 10_000),
                GivenEncodeJob(3, 5_000)
            };

            var schedule = Subject.BuildSchedule(jobs, devices, new Dictionary<string, int>(), budget: 3);

            // FIFO: the first two fill the GPU to its Max parallel; the third overflows to the CPU.
            schedule.Where(entry => entry.Device.Id == "nvenc:0").Select(entry => entry.Job.Id).Should().Equal(1, 2);
            schedule.Single(entry => entry.Device.Id == "software").Job.Id.Should().Be(3);
        }

        [Test]
        public void build_schedule_should_respect_max_parallel_in_sequential_mode()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);

            var devices = new List<TranscodeDevice>
            {
                GivenSoftwareDevice(new List<string> { "libx265" }),
                GivenNvidiaDevice(maxParallel: 2)
            };

            var jobs = new List<TranscodeJob>
            {
                GivenEncodeJob(1, 3_000),
                GivenEncodeJob(2, 2_000),
                GivenEncodeJob(3, 1_000),
                GivenEncodeJob(4, 500)
            };

            var schedule = Subject.BuildSchedule(jobs, devices, new Dictionary<string, int>(), budget: 2);

            // Both started jobs go on the GPU, which still has a free Max-parallel slot; the CPU stays
            // idle rather than the GPU's Max parallel being under-used.
            schedule.Should().HaveCount(2);
            schedule.Select(entry => entry.Device.Id).Should().OnlyContain(id => id == "nvenc:0");
            schedule.Select(entry => entry.Job.Id).Should().Equal(1, 2);
        }

        [Test]
        public void execute_should_fail_when_the_container_escapes_the_working_folder()
        {
            _job.Container = "../../../../../../../../tmp/evil";

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("escapes");
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_fail_when_the_target_bitrate_cannot_be_derived()
        {
            _job.TargetSize = 1_000;

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("bitrate");
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_abort_when_the_persisted_job_is_no_longer_queued()
        {
            _job.Status = TranscodeJobStatus.Cancelled;

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Cancelled);
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_cancel_when_the_token_is_already_cancelled()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Subject.ExecuteJob(_job, _device, cancellation.Token);
            }

            _job.Status.Should().Be(TranscodeJobStatus.Cancelled);
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void execute_should_clean_the_working_folder_when_the_job_fails()
        {
            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(new TranscodeResult { Success = false, Error = "boom" });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.Failed);

            var folder = Path.Combine(@"C:\appdata\transcode".AsOsAgnostic(), "1");
            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFolder(folder, true), Times.Once);
        }

        private void GivenQueuedJobAndDevices(TranscodeJob job, List<TranscodeDevice> devices, int maxConcurrent = 4)
        {
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetByStatus(TranscodeJobStatus.Queued))
                  .Returns(new List<TranscodeJob> { job });

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(maxConcurrent);
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(false);

            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(devices);
        }

        private static TranscodeDevice GivenSoftwareDevice(List<string> encoders)
        {
            return new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Name = "CPU",
                Supported = true,
                Enabled = true,
                MaxParallel = 1,
                Priority = 100,
                Capabilities = new DeviceCapability { Encoders = encoders ?? new List<string>() }
            };
        }

        [Test]
        public void schedule_once_should_fail_a_job_no_enabled_device_can_encode()
        {
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { GivenSoftwareDevice(new List<string>()) });

            Subject.ScheduleFailureGrace = TimeSpan.Zero;
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("No enabled device");
        }

        [Test]
        public void schedule_once_should_wait_while_a_capable_device_is_unavailable()
        {
            var unavailable = GivenSoftwareDevice(new List<string> { "libx265" });
            unavailable.Unavailable = true;

            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { unavailable });

            // A capable-but-unavailable device must not fail the job before the (longer) pending
            // grace elapses, even though the shorter codec grace has.
            Subject.ScheduleFailureGrace = TimeSpan.Zero;
            Subject.PendingDeviceGrace = TimeSpan.FromMinutes(5);
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Queued);
        }

        [Test]
        public void schedule_once_should_wait_when_the_capable_device_is_disabled()
        {
            var disabled = GivenSoftwareDevice(new List<string> { "libx265" });
            disabled.Enabled = false;

            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { disabled });

            Subject.ScheduleFailureGrace = TimeSpan.Zero;
            Subject.PendingDeviceGrace = TimeSpan.FromMinutes(5);
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Queued);
        }

        [Test]
        public void schedule_once_should_wait_when_no_devices_are_detected()
        {
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice>());

            // A transient empty snapshot (e.g. before the first probe) must not fail immediately.
            Subject.ScheduleFailureGrace = TimeSpan.Zero;
            Subject.PendingDeviceGrace = TimeSpan.FromMinutes(5);
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Queued);
        }

        [Test]
        public void schedule_once_should_fail_after_the_pending_grace_when_devices_stay_disabled()
        {
            var disabled = GivenSoftwareDevice(new List<string> { "libx265" });
            disabled.Enabled = false;

            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { disabled });

            // A permanently disabled device must not leave the job Queued forever: once the pending
            // grace elapses it fails with the distinct "no enabled device" message.
            Subject.PendingDeviceGrace = TimeSpan.Zero;
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("No enabled device available");
        }

        [Test]
        public void device_cooldown_should_skip_a_recently_failed_device_and_expire()
        {
            Subject.DeviceCooldown = TimeSpan.FromMinutes(5);

            Subject.RecordDeviceFailure("software");
            Subject.IsDeviceCooling("software").Should().BeTrue();
            Subject.IsDeviceCooling("other").Should().BeFalse();
            Subject.IsDeviceCooling(null).Should().BeFalse();

            Subject.DeviceCooldown = TimeSpan.Zero;
            Subject.IsDeviceCooling("software").Should().BeFalse();
        }

        [Test]
        public void schedule_once_should_skip_a_cooling_device_and_use_the_next_capable_one()
        {
            var gpu = GivenSoftwareDevice(new List<string> { "libx265" });
            gpu.Id = "gpu";
            gpu.Name = "GPU";
            gpu.Priority = 1;

            var cpu = GivenSoftwareDevice(new List<string> { "libx265" });
            cpu.Id = "cpu";
            cpu.Name = "CPU";
            cpu.Priority = 2;

            _job.DeviceId = "gpu";
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { gpu, cpu });

            Subject.DeviceCooldown = TimeSpan.FromMinutes(5);
            Subject.RecordDeviceFailure("gpu");

            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(new TranscodeResult { Success = true, OutputPath = "/tmp/out.mkv", OutputSize = 123 });

            Subject.ScheduleOnce();

            SpinWait.SpinUntil(() => _job.Status != TranscodeJobStatus.Queued, TimeSpan.FromSeconds(5)).Should().BeTrue();
            _job.DeviceId.Should().Be("cpu");
        }

        [Test]
        public void schedule_once_should_fail_after_the_pending_grace_when_no_devices_are_detected()
        {
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice>());

            Subject.PendingDeviceGrace = TimeSpan.Zero;
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Failed);
            _job.Error.Should().Contain("No enabled device available");
        }

        [Test]
        public void schedule_once_should_not_fail_a_job_cancelled_after_the_snapshot()
        {
            // The scheduler snapshots Queued rows and re-reads under the per-job gate. Simulate a
            // cancel that landed between the two: the re-read must win and the job must stay
            // Cancelled (never overwritten with Failed).
            var cancelled = new TranscodeJob { Id = _job.Id, Status = TranscodeJobStatus.Cancelled };
            var finds = 0;

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(_job.Id))
                  .Returns(() => finds++ == 0 ? _job : cancelled);

            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { GivenSoftwareDevice(new List<string>()) });

            Subject.ScheduleFailureGrace = TimeSpan.Zero;
            Subject.PendingDeviceGrace = TimeSpan.Zero;
            Subject.ScheduleOnce();

            _job.Status.Should().Be(TranscodeJobStatus.Queued);
        }

        [Test]
        public void cancel_in_the_claim_window_keeps_the_job_cancelled()
        {
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { _device });

            Subject.OnJobClaimed = job => Subject.CancelJob(job.Id);

            Subject.ScheduleOnce();

            SpinWait.SpinUntil(() => _job.Status == TranscodeJobStatus.Cancelled, TimeSpan.FromSeconds(2)).Should().BeTrue();
            _job.Status.Should().Be(TranscodeJobStatus.Cancelled);
            Mocker.GetMock<ITranscodeRunner>()
                  .Verify(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void cancel_should_not_throw_when_it_races_the_worker_teardown()
        {
            GivenQueuedJobAndDevices(_job, new List<TranscodeDevice> { _device });

            using var started = new ManualResetEventSlim(false);
            using var releaseRunner = new ManualResetEventSlim(false);

            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(() =>
                  {
                      started.Set();
                      releaseRunner.Wait(TimeSpan.FromSeconds(5));
                      return new TranscodeResult { Success = true, OutputPath = "/tmp/out.mkv", OutputSize = 1 };
                  });

            var failures = new List<Exception>();

            // Hammer CancelJob while the worker finishes and tears down (removing and disposing the
            // CTS). A read-then-release race would surface as an ObjectDisposedException here.
            var canceller = Task.Run(() =>
            {
                if (!started.Wait(TimeSpan.FromSeconds(5)))
                {
                    return;
                }

                var deadline = DateTime.UtcNow.AddMilliseconds(750);

                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        Subject.CancelJob(_job.Id);
                    }
                    catch (Exception ex)
                    {
                        lock (failures)
                        {
                            failures.Add(ex);
                        }

                        return;
                    }
                }
            });

            Subject.ScheduleOnce();

            started.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            releaseRunner.Set();
            canceller.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            lock (failures)
            {
                failures.Should().BeEmpty();
            }
        }

        [Test]
        public void blocked_jobs_should_not_include_a_running_remux()
        {
            GivenRunning(new TranscodeJob { Id = 9, Mode = TranscodeMode.Remux, DeviceId = TranscodeService.RemuxDeviceId, StartedAt = DateTime.UtcNow });

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(3);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { GivenDevice(false, 1) });

            Subject.GetJobsExceedingSettings().Should().BeEmpty();
        }

        [Test]
        public void blocked_jobs_should_include_a_remux_over_the_global_limit()
        {
            var first = new TranscodeJob { Id = 1, DeviceId = "software", StartedAt = DateTime.UtcNow };
            var remux = new TranscodeJob { Id = 2, Mode = TranscodeMode.Remux, DeviceId = null, StartedAt = DateTime.UtcNow.AddSeconds(1) };
            var third = new TranscodeJob { Id = 3, DeviceId = "software", StartedAt = DateTime.UtcNow.AddSeconds(2) };

            GivenRunning(first, remux, third);

            Mocker.GetMock<IConfigService>().SetupGet(service => service.MaxConcurrentJobs).Returns(2);
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { GivenDevice(true, 10) });

            Subject.GetJobsExceedingSettings().Select(job => job.Id).Should().Equal(3);
        }

        [Test]
        public void resolve_should_reject_a_second_resolve_of_the_same_job()
        {
            GivenReviewableJob();

            Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<InvalidOperationException>().WithMessage("*not awaiting review*");
        }

        [Test]
        public void resolve_should_not_serialize_unrelated_jobs_that_share_a_stripe()
        {
            GivenReviewableJob();

            // Ids 1 and 1 + LockStripeCount hash to the same resolve-lock stripe.
            var otherId = 1 + TranscodeService.LockStripeCount;
            var other = new TranscodeJob
            {
                Id = otherId,
                SourcePath = @"C:\media\other.mkv".AsOsAgnostic(),
                OutputPath = @"C:\appdata\transcode\65\out.mkv".AsOsAgnostic(),
                OutputSize = 500,
                Status = TranscodeJobStatus.AwaitingReview,
                MediaType = MediaType.Series,
                EpisodeFileId = 20
            };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Get(otherId)).Returns(other);
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(otherId)).Returns(other);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.FileExists(other.OutputPath)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.GetFileSize(other.OutputPath)).Returns(500);
            Mocker.GetMock<IMediaFileService>().Setup(service => service.Get(20)).Returns(new EpisodeFile { Id = 20, RelativePath = "other.mkv" });

            using var transferStarted = new ManualResetEventSlim(false);
            using var releaseTransfer = new ManualResetEventSlim(false);

            var firstStaging = _job.SourcePath + ".1.transcode~";

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, firstStaging, TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Callback(() =>
                  {
                      transferStarted.Set();
                      releaseTransfer.Wait(TimeSpan.FromSeconds(10));
                  });

            var first = Task.Run(() => Subject.Resolve(1, TranscodeReviewAction.Overwrite));

            transferStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            try
            {
                // The second job shares the stripe but must not wait behind the first's multi-GB copy.
                var second = Task.Run(() => Subject.Resolve(otherId, TranscodeReviewAction.Overwrite));

                second.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                second.Result.Status.Should().Be(TranscodeJobStatus.Completed);
            }
            finally
            {
                releaseTransfer.Set();
            }

            first.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            first.Result.Status.Should().Be(TranscodeJobStatus.Completed);
        }

        [Test]
        public void resolve_should_report_a_hostile_destination_without_a_bare_error()
        {
            GivenReviewableJob();

            _job.Tag = "x/../../../../evil";

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<InvalidOperationException>().WithMessage("*destination*");
            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            _job.Error.Should().Contain("destination");
        }

        [Test]
        public void transfer_should_not_wait_forever_for_a_stuck_progress_watcher()
        {
            GivenReviewableJob();

            Subject.TransferWatcherStopTimeout = TimeSpan.FromMilliseconds(100);

            var staging = _job.SourcePath + ".1.transcode~";

            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.FileExists(staging)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.GetFileSize(staging)).Returns(250);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.GetFileSize(_job.OutputPath)).Returns(1000);

            using var watcherEnteredFind = new ManualResetEventSlim(false);
            using var releaseWatcher = new ManualResetEventSlim(false);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(1))
                  .Returns(() =>
                  {
                      watcherEnteredFind.Set();
                      releaseWatcher.Wait(TimeSpan.FromSeconds(10));
                      return _job;
                  });

            // Hold the copy open until the watcher is inside the (blocking) repository call, so the
            // watcher is guaranteed to still be running when the transfer cancels it.
            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Callback(() => watcherEnteredFind.Wait(TimeSpan.FromSeconds(5)));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var job = Subject.Resolve(1, TranscodeReviewAction.Overwrite);
            stopwatch.Stop();

            releaseWatcher.Set();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            job.Status.Should().Be(TranscodeJobStatus.Completed);
        }

        [Test]
        public void transfer_watcher_should_not_lower_the_progress_of_a_completed_row()
        {
            GivenReviewableJob();
            _job.Progress = 0;

            Subject.TransferWatcherStopTimeout = TimeSpan.FromMilliseconds(50);

            var staging = _job.SourcePath + ".1.transcode~";

            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.FileExists(staging)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.GetFileSize(staging)).Returns(500);
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.GetFileSize(_job.OutputPath)).Returns(1000);

            // A stale repository read: the watcher's early-out view of the row still says
            // Transferring, even though the shared job object has already been completed.
            var stale = new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Transferring };

            using var watcherEnteredFind = new ManualResetEventSlim(false);
            using var releaseWatcher = new ManualResetEventSlim(false);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(1))
                  .Returns(() =>
                  {
                      watcherEnteredFind.Set();
                      releaseWatcher.Wait(TimeSpan.FromSeconds(10));
                      return stale;
                  });

            // Hold the copy until the watcher has evaluated its progress condition and is inside the
            // (blocking) repository early-out, so the resolve completes before the progress write.
            Mocker.GetMock<IDiskTransferService>()
                  .Setup(transfer => transfer.TransferFile(_job.OutputPath, staging, TransferMode.Copy, true, It.IsAny<CancellationToken>()))
                  .Callback(() => watcherEnteredFind.Wait(TimeSpan.FromSeconds(5)));

            var job = Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            releaseWatcher.Set();

            job.Status.Should().Be(TranscodeJobStatus.Completed);

            // The guarded write must reject the stale view: a completed row keeps progress 100.
            job.Progress.Should().Be(100);
        }

        [Test]
        public void handle_should_continue_past_a_hostile_transferring_row_and_still_recover()
        {
            var hostile = new TranscodeJob
            {
                Id = 7,
                SourcePath = @"C:\media\episode.mkv".AsOsAgnostic(),
                Tag = "x/../../../../evil",
                Mode = TranscodeMode.TargetSize,
                Status = TranscodeJobStatus.Transferring,
                OutputPath = @"C:\appdata\transcode\7\out.mkv".AsOsAgnostic()
            };

            var recoverable = new TranscodeJob
            {
                Id = 8,
                SourcePath = @"C:\media\other.mkv".AsOsAgnostic(),
                Mode = TranscodeMode.TargetSize,
                Status = TranscodeJobStatus.Transferring,
                OutputPath = @"C:\appdata\transcode\8\out.mkv".AsOsAgnostic()
            };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running)).Returns(new List<TranscodeJob>());
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Transferring)).Returns(new List<TranscodeJob> { hostile, recoverable });
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Queued)).Returns(new List<TranscodeJob>());

            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.FileExists(recoverable.OutputPath)).Returns(true);

            Action act = () => Subject.Handle(new ApplicationStartedEvent());

            act.Should().NotThrow();

            // The bad row must not abort the recovery loop before the healthy row is returned to review.
            recoverable.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
        }

        [Test]
        public void handle_should_start_when_recovery_throws()
        {
            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetByStatus(It.IsAny<TranscodeJobStatus>()))
                  .Throws(new IOException("database unavailable"));

            Action act = () => Subject.Handle(new ApplicationStartedEvent());

            act.Should().NotThrow();
        }

        [Test]
        public void resolve_gate_should_be_a_bounded_stripe()
        {
            var first = Subject.GetResolveLock(1);

            Subject.GetResolveLock(1).Should().BeSameAs(first);
            Subject.GetResolveLock(1 + TranscodeService.LockStripeCount).Should().BeSameAs(first);
            Subject.GetResolveLock(2).Should().NotBeSameAs(first);
        }

        [Test]
        public void is_inside_directory_should_follow_the_platform_case_sensitivity()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TheoriarrTranscode");
            var inside = Path.Combine(directory, "episode.mkv");
            var differingCase = Path.Combine(Path.GetTempPath(), "theoriarrtranscode", "episode.mkv");

            TranscodeService.IsInsideDirectory(directory, inside).Should().BeTrue();
            TranscodeService.IsInsideDirectory(directory, differingCase).Should().Be(OperatingSystem.IsWindows());
        }

        [Test]
        public void resolve_should_abort_when_the_transferring_write_fails()
        {
            GivenReviewableJob();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Update(It.IsAny<TranscodeJob>()))
                  .Throws(new InvalidOperationException("database is read-only"));

            Action act = () => Subject.Resolve(1, TranscodeReviewAction.Overwrite);

            act.Should().Throw<InvalidOperationException>();

            // The claim could not be persisted, so the row is not Transferring and no copy starts.
            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void recovery_should_isolate_a_row_whose_persist_fails()
        {
            var bad = new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Running };
            var good = new TranscodeJob { Id = 2, Status = TranscodeJobStatus.Running };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running)).Returns(new List<TranscodeJob> { bad, good });
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Transferring)).Returns(new List<TranscodeJob>());

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Update(It.Is<TranscodeJob>(job => job.Id == 1)))
                  .Throws(new IOException("database is locked"));

            Action act = () => Subject.Handle(new ApplicationStartedEvent());

            act.Should().NotThrow();

            // A failed write must not abort the pass before the healthy row is recovered.
            good.Status.Should().Be(TranscodeJobStatus.Failed);
        }

        [Test]
        public void reconcile_should_fail_a_stranded_running_job()
        {
            var stranded = new TranscodeJob
            {
                Id = 1,
                Status = TranscodeJobStatus.Running,
                LastUpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running)).Returns(new List<TranscodeJob> { stranded });
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Transferring)).Returns(new List<TranscodeJob>());

            Subject.StrandedJobGrace = TimeSpan.FromMinutes(1);
            Subject.ReconcileStrandedJobs();

            stranded.Status.Should().Be(TranscodeJobStatus.Failed);
            stranded.Error.Should().Contain("Interrupted");
        }

        [Test]
        public void reconcile_should_leave_a_recently_updated_running_job_alone()
        {
            var recent = new TranscodeJob
            {
                Id = 1,
                Status = TranscodeJobStatus.Running,
                LastUpdatedAt = DateTime.UtcNow
            };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running)).Returns(new List<TranscodeJob> { recent });
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Transferring)).Returns(new List<TranscodeJob>());

            Subject.StrandedJobGrace = TimeSpan.FromMinutes(1);
            Subject.ReconcileStrandedJobs();

            recent.Status.Should().Be(TranscodeJobStatus.Running);
        }

        [Test]
        public void reconcile_should_return_a_stranded_transfer_to_review_when_the_output_survived()
        {
            var stranded = new TranscodeJob
            {
                Id = 1,
                Status = TranscodeJobStatus.Transferring,
                SourcePath = @"C:\media\episode.mkv".AsOsAgnostic(),
                OutputPath = @"C:\appdata\transcode\1\out.mkv".AsOsAgnostic(),
                LastUpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Running)).Returns(new List<TranscodeJob>());
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.GetByStatus(TranscodeJobStatus.Transferring)).Returns(new List<TranscodeJob> { stranded });
            Mocker.GetMock<IDiskProvider>().Setup(disk => disk.FileExists(stranded.OutputPath)).Returns(true);

            Subject.StrandedJobGrace = TimeSpan.FromMinutes(1);
            Subject.ReconcileStrandedJobs();

            stranded.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            stranded.OutputPath.Should().NotBeNull();
        }

        [Test]
        public void cancel_should_clear_stale_progress_on_the_terminal_row()
        {
            _job.Status = TranscodeJobStatus.Queued;
            _job.Progress = 42;
            _job.Speed = "12.0x";
            _job.Fps = "30";
            _job.Eta = "00:01:00";
            _job.OutputPath = @"C:\appdata\transcode\1\out.mkv".AsOsAgnostic();
            _job.OutputSize = 123;

            var result = Subject.CancelJob(1);

            result.Should().Be(CancelJobResult.Cancelled);
            _job.Status.Should().Be(TranscodeJobStatus.Cancelled);
            _job.Speed.Should().BeNull();
            _job.Fps.Should().BeNull();
            _job.Eta.Should().BeNull();
            _job.OutputPath.Should().BeNull();
            _job.OutputSize.Should().BeNull();
        }

        [Test]
        public void execute_should_survive_a_throwing_event_subscriber()
        {
            Mocker.GetMock<IEventAggregator>()
                  .Setup(aggregator => aggregator.PublishEvent(It.IsAny<TranscodeProgressEvent>()))
                  .Throws(new InvalidOperationException("subscriber blew up"));

            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Returns(new TranscodeResult { Success = true, OutputPath = "/tmp/out.mkv", OutputSize = 1 });

            Action act = () => Subject.ExecuteJob(_job, _device);

            act.Should().NotThrow();
            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
        }

        [Test]
        public void progress_callback_should_not_overwrite_a_row_that_is_no_longer_running()
        {
            Action<TranscodeProgress> onProgress = null;

            Mocker.GetMock<ITranscodeRunner>()
                  .Setup(runner => runner.Run(It.IsAny<TranscodePlan>(), It.IsAny<Action<TranscodeProgress>>(), It.IsAny<CancellationToken>()))
                  .Callback((TranscodePlan plan, Action<TranscodeProgress> callback, CancellationToken token) => onProgress = callback)
                  .Returns(new TranscodeResult { Success = true, OutputPath = "/tmp/out.mkv", OutputSize = 1 });

            Subject.ExecuteJob(_job, _device);

            _job.Status.Should().Be(TranscodeJobStatus.AwaitingReview);
            var progressBefore = _job.Progress;

            // A late runner tick (Completed bypasses the throttle) must be rejected now the row has
            // moved on, instead of lowering a row that already reached review.
            onProgress(new TranscodeProgress { Completed = true, Percent = 42, Speed = "5x", Fps = "30", Eta = "00:01:00" });

            _job.Progress.Should().Be(progressBefore);
            _job.Speed.Should().BeNull();
        }

        [Test]
        public void cancel_should_report_not_found_for_an_unknown_id()
        {
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(999)).Returns((TranscodeJob)null);

            Subject.CancelJob(999).Should().Be(CancelJobResult.NotFound);
        }

        [Test]
        public void cancel_should_report_not_cancellable_for_a_terminal_job()
        {
            _job.Status = TranscodeJobStatus.Completed;

            Subject.CancelJob(1).Should().Be(CancelJobResult.NotCancellable);
            _job.Status.Should().Be(TranscodeJobStatus.Completed);
        }

        [Test]
        public void cancel_should_report_cancelled_for_a_queued_job()
        {
            _job.Status = TranscodeJobStatus.Queued;

            Subject.CancelJob(1).Should().Be(CancelJobResult.Cancelled);
            _job.Status.Should().Be(TranscodeJobStatus.Cancelled);
        }

        [Test]
        public void force_stop_jobs_should_only_return_jobs_it_actually_cancelled()
        {
            var queued = new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Queued };
            var completed = new TranscodeJob { Id = 6, Status = TranscodeJobStatus.Completed };

            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(5)).Returns(queued);
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(6)).Returns(completed);
            Mocker.GetMock<ITranscodeJobRepository>().Setup(repository => repository.Find(999)).Returns((TranscodeJob)null);

            // An unknown (999) and a terminal (6) id are no-ops, not reported stops.
            var stopped = Subject.ForceStopJobs(new[] { 999, 5, 6 });

            stopped.Select(job => job.Id).Should().Equal(5);
        }

        [Test]
        public void build_schedule_should_order_by_priority_then_id()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.PreferHardware).Returns(true);

            var devices = new List<TranscodeDevice> { GivenNvidiaDevice(maxParallel: 3) };

            var low = GivenEncodeJob(1, 1_000);
            var high = GivenEncodeJob(2, 1_000);
            high.Priority = 5;
            var mid = GivenEncodeJob(3, 1_000);
            mid.Priority = 5;

            var schedule = Subject.BuildSchedule(new List<TranscodeJob> { low, high, mid }, devices, new Dictionary<string, int>(), budget: 3);

            schedule.Select(entry => entry.Job.Id).Should().Equal(2, 3, 1);
        }
    }
}
