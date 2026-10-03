using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class GpuCapabilityServiceFixture : CoreTest
    {
        private static DeviceCapability GivenCapability(string id, TranscodeDeviceKind kind, bool supported = true, int maxSessions = 8, string[] encoders = null)
        {
            return new DeviceCapability
            {
                Id = id,
                Kind = kind,
                Name = id,
                Supported = supported,
                MaxSessions = maxSessions,
                Encoders = encoders?.ToList() ?? new List<string>()
            };
        }

        [Test]
        public void merge_should_enable_supported_devices_and_default_controls()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("software", TranscodeDeviceKind.Software),
                GivenCapability("nvenc:0", TranscodeDeviceKind.Nvidia),
                GivenCapability("vaapi:/dev/dri/renderD128", TranscodeDeviceKind.Vaapi)
            };

            var devices = GpuCapabilityService.Merge(detected, new List<TranscodeDevice>());

            devices.Should().HaveCount(3);

            var software = devices.Single(device => device.Id == "software");
            software.Enabled.Should().BeTrue();
            software.MaxParallel.Should().Be(1);
            software.Priority.Should().Be(100);

            var nvenc = devices.Single(device => device.Id == "nvenc:0");
            nvenc.Enabled.Should().BeTrue();
            nvenc.MaxParallel.Should().Be(2);
            nvenc.Priority.Should().Be(10);

            var vaapi = devices.Single(device => device.Id == "vaapi:/dev/dri/renderD128");
            vaapi.Enabled.Should().BeTrue();
            vaapi.MaxParallel.Should().Be(1);
            vaapi.Priority.Should().Be(20);
        }

        [Test]
        public void merge_should_disable_unsupported_devices_by_default()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("vaapi:/dev/dri/renderD129", TranscodeDeviceKind.Vaapi, supported: false)
            };

            var devices = GpuCapabilityService.Merge(detected, new List<TranscodeDevice>());

            devices.Single().Enabled.Should().BeFalse();
        }

        [Test]
        public void merge_should_preserve_user_control_settings()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("nvenc:0", TranscodeDeviceKind.Nvidia)
            };

            var stored = new List<TranscodeDevice>
            {
                new TranscodeDevice
                {
                    Id = "nvenc:0",
                    Enabled = false,
                    MaxParallel = 5,
                    Priority = 7,
                    Weight = 42
                }
            };

            var device = GpuCapabilityService.Merge(detected, stored).Single();

            device.Enabled.Should().BeFalse();
            device.MaxParallel.Should().Be(5);
            device.Priority.Should().Be(7);
            device.Weight.Should().Be(42);
            device.Supported.Should().BeTrue();
        }

        [Test]
        public void merge_should_mark_missing_devices_unavailable()
        {
            var stored = new List<TranscodeDevice>
            {
                new TranscodeDevice { Id = "nvenc:1", Enabled = true, MaxParallel = 2, Priority = 10, Weight = 100 }
            };

            var device = GpuCapabilityService.Merge(new List<DeviceCapability>(), stored).Single();

            device.Unavailable.Should().BeTrue();
            device.Enabled.Should().BeTrue();
        }

        [Test]
        public void merge_should_enable_supported_codecs_and_disable_unsupported_ones()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("nvenc:0", TranscodeDeviceKind.Nvidia, encoders: new[] { "h264_nvenc", "hevc_nvenc" })
            };

            var device = GpuCapabilityService.Merge(detected, new List<TranscodeDevice>()).Single();

            device.Codecs.Should().HaveCount(3);

            var h264 = device.Codecs.Single(codec => codec.Codec == "h264");
            h264.Supported.Should().BeTrue();
            h264.Enabled.Should().BeTrue();
            h264.Encoder.Should().Be("h264_nvenc");

            var hevc = device.Codecs.Single(codec => codec.Codec == "hevc");
            hevc.Supported.Should().BeTrue();
            hevc.Enabled.Should().BeTrue();

            var av1 = device.Codecs.Single(codec => codec.Codec == "av1");
            av1.Supported.Should().BeFalse();
            av1.Enabled.Should().BeFalse();
            av1.Encoder.Should().Be("av1_nvenc");
        }

        [Test]
        public void merge_should_preserve_a_forced_unsupported_codec()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("nvenc:0", TranscodeDeviceKind.Nvidia, encoders: new[] { "hevc_nvenc" })
            };

            var stored = new List<TranscodeDevice>
            {
                new TranscodeDevice
                {
                    Id = "nvenc:0",
                    Codecs = new List<TranscodeCodecSetting>
                    {
                        new TranscodeCodecSetting { Codec = "av1", Supported = false, Enabled = true }
                    }
                }
            };

            var av1 = GpuCapabilityService.Merge(detected, stored).Single().Codecs.Single(codec => codec.Codec == "av1");

            av1.Supported.Should().BeFalse();
            av1.Enabled.Should().BeTrue();
        }

        [Test]
        public void merge_should_follow_fresh_detection_when_the_stored_choice_was_the_default()
        {
            var detected = new List<DeviceCapability>
            {
                GivenCapability("nvenc:0", TranscodeDeviceKind.Nvidia, encoders: new[] { "av1_nvenc" })
            };

            var stored = new List<TranscodeDevice>
            {
                new TranscodeDevice
                {
                    Id = "nvenc:0",
                    Codecs = new List<TranscodeCodecSetting>
                    {
                        // Left at the old default (off because it was unsupported); now supported it turns on.
                        new TranscodeCodecSetting { Codec = "av1", Supported = false, Enabled = false }
                    }
                }
            };

            var av1 = GpuCapabilityService.Merge(detected, stored).Single().Codecs.Single(codec => codec.Codec == "av1");

            av1.Supported.Should().BeTrue();
            av1.Enabled.Should().BeTrue();
            av1.Encoder.Should().Be("av1_nvenc");
        }
    }

    [TestFixture]
    public class GpuCapabilityServiceCacheFixture : CoreTest<GpuCapabilityService>
    {
        private Mock<IHardwareAccelBackend> _backend;

        [SetUp]
        public void Setup()
        {
            _backend = new Mock<IHardwareAccelBackend>();
            _backend.Setup(backend => backend.Kind).Returns(TranscodeDeviceKind.Nvidia);
            _backend.Setup(backend => backend.GetFingerprint()).Returns("gpu-fingerprint");
            _backend.Setup(backend => backend.Detect()).Returns(new List<DeviceCapability>
            {
                new DeviceCapability
                {
                    Id = "nvenc:0",
                    Kind = TranscodeDeviceKind.Nvidia,
                    Name = "GPU",
                    Supported = true,
                    MaxSessions = 8,
                    Encoders = new List<string> { "h264_nvenc" }
                }
            });

            Mocker.SetConstant<IEnumerable<IHardwareAccelBackend>>(new[] { _backend.Object });

            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(provider => provider.GetFFmpegPath())
                  .Returns("/usr/bin/ffmpeg");

            Mocker.GetMock<IFFmpegInfo>()
                  .Setup(info => info.GetVersion())
                  .Returns("6.0");
        }

        [Test]
        public void should_reuse_the_cached_snapshot_without_recomputing_the_fingerprint()
        {
            Subject.GetCapabilities();
            Subject.GetCapabilities();
            Subject.GetCapabilities();

            _backend.Verify(backend => backend.GetFingerprint(), Times.Once);
            _backend.Verify(backend => backend.Detect(), Times.Once);
            Mocker.GetMock<IFFmpegInfo>().Verify(info => info.GetVersion(), Times.Exactly(2));
        }

        [Test]
        public void should_reprobe_on_force()
        {
            Subject.GetCapabilities();

            Subject.GetCapabilities(force: true);

            _backend.Verify(backend => backend.GetFingerprint(), Times.Exactly(2));
            _backend.Verify(backend => backend.Detect(), Times.Exactly(2));
        }

        [Test]
        public void should_hand_out_a_defensive_copy_of_the_devices()
        {
            var device = Subject.GetDevices().Single(candidate => candidate.Id == "nvenc:0");

            device.Name = "mutated";
            device.Enabled = !device.Enabled;
            device.MaxParallel = 99;
            device.Capabilities.Encoders.Add("injected");
            device.Codecs.Clear();

            var cached = Subject.GetDevices().Single(candidate => candidate.Id == "nvenc:0");

            cached.Name.Should().Be("GPU");
            cached.MaxParallel.Should().NotBe(99);
            cached.Capabilities.Encoders.Should().NotContain("injected");
            cached.Codecs.Should().NotBeEmpty();
        }

        [Test]
        public void should_hand_out_a_defensive_copy_of_the_capabilities()
        {
            var first = Subject.GetCapabilities();
            first.Devices.Single(candidate => candidate.Id == "nvenc:0").Name = "mutated";
            first.Devices.Clear();

            var cached = Subject.GetCapabilities();

            cached.Devices.Should().NotBeEmpty();
            cached.Devices.Single(candidate => candidate.Id == "nvenc:0").Name.Should().Be("GPU");
        }

        [Test]
        public void should_not_alias_the_callers_options_into_the_cache()
        {
            var options = new TranscodeDeviceOptions { Cpu = new CpuTranscodeOptions { Threads = 3 } };

            Subject.UpdateDevices(new List<TranscodeDevice>
            {
                new TranscodeDevice
                {
                    Id = "nvenc:0",
                    Enabled = true,
                    MaxParallel = 1,
                    Priority = 10,
                    Weight = 100,
                    Options = options
                }
            });

            options.Cpu.Threads = 99;

            Subject.GetDevices().Single(device => device.Id == "nvenc:0").Options.Cpu.Threads.Should().Be(3);
        }
    }
}
