using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    // Real ffmpeg end-to-end: generates a source clip, transcodes it on the CPU and asserts the
    // output is smaller. Excluded from ./test.sh (Category=ManualTest); run explicitly.
    [TestFixture]
    [Category("ManualTest")]
    public class FFmpegTranscodeRunnerIntegrationFixture : CoreTest<FFmpegTranscodeRunner>
    {
        private string _folder;

        [SetUp]
        public void Setup()
        {
            _folder = Path.Combine(Path.GetTempPath(), "theoriarr-transcode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);

            var disk = Mocker.GetMock<IDiskProvider>();
            disk.Setup(provider => provider.FileExists(It.IsAny<string>())).Returns<string>(File.Exists);
            disk.Setup(provider => provider.FolderExists(It.IsAny<string>())).Returns<string>(Directory.Exists);
            disk.Setup(provider => provider.CreateFolder(It.IsAny<string>())).Callback<string>(path => Directory.CreateDirectory(path));
            disk.Setup(provider => provider.GetFileSize(It.IsAny<string>())).Returns<string>(path => new FileInfo(path).Length);
            disk.Setup(provider => provider.DeleteFile(It.IsAny<string>())).Callback<string>(path => File.Delete(path));
            disk.Setup(provider => provider.GetAvailableSpace(It.IsAny<string>())).Returns(long.MaxValue);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(service => service.FFmpegPath)
                  .Returns(string.Empty);

            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(info => info.StartUpFolder)
                  .Returns(AppContext.BaseDirectory);

            var processProvider = new ProcessProvider(TestLogger);

            Mocker.SetConstant<IProcessProvider>(processProvider);
            Mocker.SetConstant<IFFmpegProvider>(new FFmpegProvider(
                Mocker.GetMock<IConfigService>().Object,
                Mocker.GetMock<IAppFolderInfo>().Object,
                disk.Object,
                processProvider,
                TestLogger));
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void should_transcode_a_real_file_and_report_progress()
        {
            var ffmpeg = Mocker.Resolve<IFFmpegProvider>().GetFFmpegPath();

            if (ffmpeg == null)
            {
                Assert.Ignore("ffmpeg is not available on this host");
            }

            var source = Path.Combine(_folder, "source.mkv");
            var output = Path.Combine(_folder, "output.mkv");

            Mocker.Resolve<IProcessProvider>()
                  .StartAndCapture(ffmpeg, $"-hide_banner -nostdin -y -f lavfi -i testsrc2=size=320x240:rate=15:duration=4 -c:v libx264 -preset ultrafast -an {source}")
                  .ExitCode.Should().Be(0);

            var job = new TranscodeJob
            {
                Id = 1,
                SourcePath = source,
                Mode = TranscodeMode.Quality,
                QualityValue = 30,
                Preset = "ultrafast",
                VideoCodec = "hevc"
            };

            var device = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Supported = true,
                Enabled = true,
                Capabilities = new DeviceCapability
                {
                    Id = "software",
                    Kind = TranscodeDeviceKind.Software,
                    Supported = true,
                    Encoders = new List<string> { "libx264", "libx265" }
                }
            };

            var plan = Mocker.Resolve<TranscodeArgumentBuilder>()
                              .Build(job, device, TranscodeCodec.Hevc, 0, 4, null, output);

            var progressReports = 0;

            var result = Mocker.Resolve<FFmpegTranscodeRunner>()
                               .Run(plan, _ => Interlocked.Increment(ref progressReports), CancellationToken.None);

            result.Success.Should().BeTrue(result.Error);
            File.Exists(output).Should().BeTrue();
            new FileInfo(output).Length.Should().BeGreaterThan(0);
            progressReports.Should().BeGreaterThan(0);
        }

        [Test]
        public void should_transcode_on_a_vaapi_device_when_available()
        {
            var ffmpeg = Mocker.Resolve<IFFmpegProvider>().GetFFmpegPath();

            if (ffmpeg == null)
            {
                Assert.Ignore("ffmpeg is not available on this host");
            }

            var vaapiNode = FindVaapiEncodeNode();

            if (vaapiNode == null)
            {
                Assert.Ignore("No VA-API device with encode support on this host");
            }

            var source = Path.Combine(_folder, "source.mkv");
            var output = Path.Combine(_folder, "output.mkv");

            Mocker.Resolve<IProcessProvider>()
                  .StartAndCapture(ffmpeg, $"-hide_banner -nostdin -y -f lavfi -i testsrc2=size=320x240:rate=15:duration=4 -c:v libx264 -preset ultrafast -an {source}")
                  .ExitCode.Should().Be(0);

            var job = new TranscodeJob
            {
                Id = 2,
                SourcePath = source,
                Mode = TranscodeMode.Quality,
                QualityValue = 28,
                VideoCodec = "h264"
            };

            var device = new TranscodeDevice
            {
                Id = "vaapi:" + vaapiNode,
                Kind = TranscodeDeviceKind.Vaapi,
                Supported = true,
                Enabled = true,
                Capabilities = new DeviceCapability
                {
                    Id = "vaapi:" + vaapiNode,
                    Kind = TranscodeDeviceKind.Vaapi,
                    Supported = true,
                    Encoders = new List<string> { "h264_vaapi", "hevc_vaapi" }
                }
            };

            var plan = Mocker.Resolve<TranscodeArgumentBuilder>()
                              .Build(job, device, TranscodeCodec.H264, 0, 4, null, output);

            var result = Mocker.Resolve<FFmpegTranscodeRunner>()
                               .Run(plan, _ => { }, CancellationToken.None);

            result.Success.Should().BeTrue(result.Error);
            File.Exists(output).Should().BeTrue();
            new FileInfo(output).Length.Should().BeGreaterThan(0);
        }

        [Test]
        public void should_downscale_on_a_vaapi_device_when_available()
        {
            var ffmpeg = Mocker.Resolve<IFFmpegProvider>().GetFFmpegPath();

            if (ffmpeg == null)
            {
                Assert.Ignore("ffmpeg is not available on this host");
            }

            var vaapiNode = FindVaapiEncodeNode();

            if (vaapiNode == null)
            {
                Assert.Ignore("No VA-API device with encode support on this host");
            }

            var source = Path.Combine(_folder, "source-hd.mkv");
            var output = Path.Combine(_folder, "output-sd.mkv");

            Mocker.Resolve<IProcessProvider>()
                  .StartAndCapture(ffmpeg, $"-hide_banner -nostdin -y -f lavfi -i testsrc2=size=640x480:rate=15:duration=4 -c:v libx264 -preset ultrafast -an {source}")
                  .ExitCode.Should().Be(0);

            var job = new TranscodeJob
            {
                Id = 3,
                SourcePath = source,
                Mode = TranscodeMode.Quality,
                QualityValue = 28,
                MaxHeight = 240,
                VideoCodec = "h264"
            };

            var device = new TranscodeDevice
            {
                Id = "vaapi:" + vaapiNode,
                Kind = TranscodeDeviceKind.Vaapi,
                Supported = true,
                Enabled = true,
                Capabilities = new DeviceCapability
                {
                    Id = "vaapi:" + vaapiNode,
                    Kind = TranscodeDeviceKind.Vaapi,
                    Supported = true,
                    Encoders = new List<string> { "h264_vaapi", "hevc_vaapi" }
                }
            };

            var plan = Mocker.Resolve<TranscodeArgumentBuilder>()
                              .Build(job, device, TranscodeCodec.H264, 0, 4, new MediaInfoModel { Height = 480 }, output);

            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=240,format=nv12,hwupload");

            var result = Mocker.Resolve<FFmpegTranscodeRunner>()
                               .Run(plan, _ => { }, CancellationToken.None);

            result.Success.Should().BeTrue(result.Error);
            File.Exists(output).Should().BeTrue();
            new FileInfo(output).Length.Should().BeGreaterThan(0);
        }

        private string FindVaapiEncodeNode()
        {
            if (!Directory.Exists("/dev/dri"))
            {
                return null;
            }

            var vainfo = new VainfoInfo(Mocker.Resolve<IProcessProvider>(), TestLogger);

            foreach (var node in Directory.EnumerateFileSystemEntries("/dev/dri").OrderBy(entry => entry))
            {
                if (!Path.GetFileName(node).StartsWith("renderD", StringComparison.Ordinal))
                {
                    continue;
                }

                var info = vainfo.GetInfo(node);

                if (info != null && info.SupportsEncode)
                {
                    return node;
                }
            }

            return null;
        }
    }

    [TestFixture]
    public class FFmpegTranscodeRunnerFixture : CoreTest<FFmpegTranscodeRunner>
    {
        [Test]
        public void should_use_the_maximum_timeout_when_the_duration_is_unknown()
        {
            // With no probe the inactivity window is the real guard, so the absolute ceiling must not
            // cut a long encode off at the old fixed 2 hours.
            FFmpegTranscodeRunner.GetCommandTimeout(0).Should().Be(TimeSpan.FromHours(24));
            FFmpegTranscodeRunner.GetCommandTimeout(double.NaN).Should().Be(TimeSpan.FromHours(24));
        }

        [Test]
        public void should_scale_the_absolute_timeout_with_the_duration_and_clamp_it()
        {
            FFmpegTranscodeRunner.GetCommandTimeout(60).Should().Be(TimeSpan.FromHours(2));
            FFmpegTranscodeRunner.GetCommandTimeout(3600).Should().Be(TimeSpan.FromHours(12));
            FFmpegTranscodeRunner.GetCommandTimeout(100000).Should().Be(TimeSpan.FromHours(24));
        }

        [Test]
        public void should_not_time_out_while_progress_continues()
        {
            var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastActivity = started + TimeSpan.FromHours(20);
            var now = lastActivity + TimeSpan.FromMinutes(1);

            FFmpegTranscodeRunner.HasTimedOut(started, lastActivity, now, TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), out var inactivity)
                .Should().BeFalse();
            inactivity.Should().BeFalse();
        }

        [Test]
        public void should_time_out_after_the_inactivity_window()
        {
            var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = started + TimeSpan.FromMinutes(16);

            FFmpegTranscodeRunner.HasTimedOut(started, started, now, TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), out var inactivity)
                .Should().BeTrue();
            inactivity.Should().BeTrue();
        }

        [Test]
        public void should_time_out_at_the_absolute_ceiling_even_while_progressing()
        {
            var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = started + TimeSpan.FromHours(25);
            var lastActivity = now;

            FFmpegTranscodeRunner.HasTimedOut(started, lastActivity, now, TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), out var inactivity)
                .Should().BeTrue();
            inactivity.Should().BeFalse();
        }

        [Test]
        public void should_only_apply_nice_to_software_devices()
        {
            var software = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Options = new TranscodeDeviceOptions { Cpu = new CpuTranscodeOptions { Nice = 15 } }
            };

            var nvidia = new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia,
                Options = new TranscodeDeviceOptions { Cpu = new CpuTranscodeOptions { Nice = 15 } }
            };

            FFmpegTranscodeRunner.GetNice(software, 0).Should().Be(15);
            FFmpegTranscodeRunner.GetNice(nvidia, 15).Should().Be(0);
            FFmpegTranscodeRunner.GetNice(null, 15).Should().Be(0);
        }

        [Test]
        public void should_fall_back_to_the_global_nice_for_software_devices()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeNice).Returns(7);

            var software = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software
            };

            FFmpegTranscodeRunner.GetNice(software, 7).Should().Be(7);
            Subject.GetNice(software).Should().Be(7);
        }

        [Test]
        public void should_prefer_the_explicit_device_nice_over_the_global()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeNice).Returns(7);

            var software = new TranscodeDevice
            {
                Id = "software",
                Kind = TranscodeDeviceKind.Software,
                Options = new TranscodeDeviceOptions { Cpu = new CpuTranscodeOptions { Nice = 15 } }
            };

            Subject.GetNice(software).Should().Be(15);
        }

        [Test]
        public void should_ignore_the_global_nice_for_hardware_devices()
        {
            Mocker.GetMock<IConfigService>().SetupGet(service => service.TranscodeNice).Returns(7);

            var nvidia = new TranscodeDevice
            {
                Id = "nvenc:0",
                Kind = TranscodeDeviceKind.Nvidia
            };

            Subject.GetNice(nvidia).Should().Be(0);
            Subject.GetNice(null).Should().Be(0);
        }

        [Test]
        public void should_launch_through_nice_on_unix()
        {
            if (OsInfo.IsNotWindows)
            {
                FFmpegTranscodeRunner.GetLaunchPath("/usr/bin/ffmpeg", 15).Should().Be("nice");
                FFmpegTranscodeRunner.GetLaunchArguments("/usr/bin/ffmpeg", "-i in.mkv out.mkv", 15)
                                      .Should().Be("-n 15 \"/usr/bin/ffmpeg\" -i in.mkv out.mkv");
            }
            else
            {
                FFmpegTranscodeRunner.GetLaunchPath("/usr/bin/ffmpeg", 15).Should().Be("/usr/bin/ffmpeg");
                FFmpegTranscodeRunner.GetLaunchArguments("/usr/bin/ffmpeg", "-i in.mkv out.mkv", 15)
                                      .Should().Be("-i in.mkv out.mkv");
            }

            FFmpegTranscodeRunner.GetLaunchPath("/usr/bin/ffmpeg", 0).Should().Be("/usr/bin/ffmpeg");
            FFmpegTranscodeRunner.GetLaunchArguments("/usr/bin/ffmpeg", "-i in.mkv out.mkv", 0).Should().Be("-i in.mkv out.mkv");
        }

        [Test]
        public void should_reuse_robust_quoting_for_a_launch_path_with_a_trailing_backslash()
        {
            if (!OsInfo.IsNotWindows)
            {
                return;
            }

            // A path ending in a backslash must have that backslash doubled so it does not escape the
            // closing quote and swallow the following arguments.
            FFmpegTranscodeRunner.GetLaunchArguments(@"C:\tools\ffmpeg\", "-i in.mkv", 15)
                                  .Should().Be("-n 15 \"C:\\tools\\ffmpeg\\\\\" -i in.mkv");
        }
    }
}
