using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class NvencBackendFixture : CoreTest<NvencBackend>
    {
        [SetUp]
        public void Setup()
        {
            var nvidiaSmi = new ProcessOutput { ExitCode = 0 };
            nvidiaSmi.Lines.Add(new ProcessOutputLine(ProcessOutputLevel.Standard, "0, NVIDIA GeForce RTX 4090, 24564, 550.54.15"));

            Mocker.GetMock<IProcessProvider>()
                  .Setup(provider => provider.StartAndCapture("nvidia-smi", "--query-gpu=index,name,memory.total,driver_version --format=csv,noheader,nounits", null, false))
                  .Returns(nvidiaSmi);

            Mocker.GetMock<IFFmpegInfo>().Setup(info => info.GetEncoders()).Returns(new List<string> { "h264_nvenc" });
            Mocker.GetMock<IFFmpegInfo>().Setup(info => info.GetDecoders()).Returns(new List<string>());
            Mocker.GetMock<IFFmpegInfo>().Setup(info => info.RunProbe(It.IsAny<string>())).Returns(true);
        }

        [Test]
        public void should_detect_opencl_tonemap_from_the_full_filter_list()
        {
            // `tonemap_opencl` has no `_cuda` suffix, so filtering the list to CUDA filters first can
            // never find it; the full list must be consulted.
            Mocker.GetMock<IFFmpegInfo>()
                  .Setup(info => info.GetFilters())
                  .Returns(new List<string> { "scale_cuda", "tonemap_opencl" });

            var device = Subject.Detect().Single();

            device.Filters.Should().Contain("scale_cuda").And.NotContain("tonemap_opencl");
            device.Tonemap.Should().Be("opencl");
        }

        [Test]
        public void should_not_report_a_tonemap_when_the_filter_is_absent()
        {
            Mocker.GetMock<IFFmpegInfo>()
                  .Setup(info => info.GetFilters())
                  .Returns(new List<string> { "scale_cuda" });

            var device = Subject.Detect().Single();

            device.Tonemap.Should().BeNull();
        }
    }
}
