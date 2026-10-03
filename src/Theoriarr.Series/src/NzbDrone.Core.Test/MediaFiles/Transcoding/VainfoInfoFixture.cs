using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class VainfoInfoFixture : CoreTest<VainfoInfo>
    {
        [Test]
        public void should_parse_driver_and_encode_profiles()
        {
            var output = string.Join("\n",
                "libva info: va_openDriver() returns 0",
                "vainfo: VA-API version: 1.20 (libva 2.12.0)",
                "vainfo: Driver version: Mesa Gallium driver 25.2.8 for AMD Ryzen 7 9800X3D (radeonsi)",
                "vainfo: Supported profile and entrypoints",
                "      VAProfileH264High               : VAEntrypointVLD",
                "      VAProfileH264High               : VAEntrypointEncSlice",
                "      VAProfileHEVCMain               : VAEntrypointEncSlice");

            var result = VainfoInfo.Parse(output);

            result.Driver.Should().Be("Mesa Gallium driver 25.2.8 for AMD Ryzen 7 9800X3D (radeonsi)");
            result.Version.Should().Be("1.20");
            result.Profiles.Should().Contain(new[] { "VAProfileH264High", "VAProfileHEVCMain" });
            result.Entrypoints.Should().Contain(new[] { "VAEntrypointVLD", "VAEntrypointEncSlice" });
            result.EncodeProfiles.Should().BeEquivalentTo(new[] { "VAProfileH264High", "VAProfileHEVCMain" });
            result.SupportsEncode.Should().BeTrue();
        }

        [Test]
        public void should_parse_decode_only_bridge_without_encode_support()
        {
            var output = string.Join("\n",
                "vainfo: VA-API version: 1.24 (libva 2.24.0)",
                "vainfo: Driver version: VA-API NVDEC driver [direct backend]",
                "vainfo: Supported profile and entrypoints",
                "      VAProfileH264Main               : VAEntrypointVLD",
                "      VAProfileHEVCMain10             : VAEntrypointVLD",
                "      VAProfileNone                   : VAEntrypointVideoProc");

            var result = VainfoInfo.Parse(output);

            result.Driver.Should().Be("VA-API NVDEC driver [direct backend]");
            result.EncodeProfiles.Should().BeEmpty();
            result.SupportsEncode.Should().BeFalse();
            result.Entrypoints.Should().Contain(new[] { "VAEntrypointVLD", "VAEntrypointVideoProc" });
        }

        [Test]
        public void should_return_null_when_no_driver_is_reported()
        {
            VainfoInfo.Parse("libva info: va_openDriver() returns -1").Should().BeNull();
            VainfoInfo.Parse(null).Should().BeNull();
        }

        [Test]
        public void get_info_should_run_vainfo_against_the_render_node()
        {
            const string node = "/dev/dri/renderD128";

            var output = new ProcessOutput { ExitCode = 0 };
            output.Lines.Add(new ProcessOutputLine(ProcessOutputLevel.Standard, "vainfo: VA-API version: 1.20"));
            output.Lines.Add(new ProcessOutputLine(ProcessOutputLevel.Standard, "vainfo: Driver version: Mesa Gallium driver"));

            Mocker.GetMock<IProcessProvider>()
                  .Setup(provider => provider.StartAndCapture("vainfo", "--display drm --device " + node, null, false))
                  .Returns(output);

            var result = Subject.GetInfo(node);

            result.Driver.Should().Be("Mesa Gallium driver");
        }

        [Test]
        public void get_info_should_return_null_on_non_zero_exit()
        {
            Mocker.GetMock<IProcessProvider>()
                  .Setup(provider => provider.StartAndCapture("vainfo", "--display drm --device /dev/dri/renderD129", null, false))
                  .Returns(new ProcessOutput { ExitCode = 3 });

            Subject.GetInfo("/dev/dri/renderD129").Should().BeNull();
        }
    }
}
