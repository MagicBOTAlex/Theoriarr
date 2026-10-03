using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeDeviceLabelFixture
    {
        private static TranscodeDevice Device(TranscodeDeviceKind kind, string id, string name)
        {
            return new TranscodeDevice
            {
                Id = id,
                Kind = kind,
                Name = name
            };
        }

        [Test]
        public void nvidia_label_should_be_the_board_indexed_by_gpu()
        {
            TranscodeDeviceLabel.Build(Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "NVIDIA GeForce GTX 1080 (GPU 0)"))
                                 .Should().Be("GTX 1080:0");

            TranscodeDeviceLabel.Build(Device(TranscodeDeviceKind.Nvidia, "nvenc:1", "NVIDIA GeForce RTX 3080 (GPU 1)"))
                                 .Should().Be("RTX 3080:1");
        }

        [Test]
        public void non_nvidia_label_should_fall_back_to_the_name()
        {
            TranscodeDeviceLabel.Build(Device(TranscodeDeviceKind.Software, "software", "CPU (Intel Xeon)"))
                                 .Should().Be("CPU (Intel Xeon)");

            // AMD/Intel are not special-cased yet; the shared name is used so adding a case later
            // is the only change needed.
            TranscodeDeviceLabel.Build(Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "AMD Radeon RX 6800"))
                                 .Should().Be("AMD Radeon RX 6800");
        }

        [Test]
        public void null_device_should_build_a_null_label()
        {
            TranscodeDeviceLabel.Build(null).Should().BeNull();
        }
    }
}
