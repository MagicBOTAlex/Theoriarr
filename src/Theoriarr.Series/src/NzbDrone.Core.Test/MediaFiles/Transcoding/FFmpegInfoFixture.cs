using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class FFmpegInfoFixture : CoreTest<FFmpegInfo>
    {
        private const string FFmpegPath = "/usr/bin/ffmpeg";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(provider => provider.GetFFmpegPath())
                  .Returns(FFmpegPath);
        }

        private void GivenOutput(string arguments, params string[] lines)
        {
            GivenOutputForPath(FFmpegPath, arguments, lines);
        }

        private void GivenOutputForPath(string path, string arguments, params string[] lines)
        {
            var output = new ProcessOutput { ExitCode = 0 };

            foreach (var line in lines)
            {
                output.Lines.Add(new ProcessOutputLine(ProcessOutputLevel.Standard, line));
            }

            Mocker.GetMock<IProcessProvider>()
                  .Setup(provider => provider.StartAndCapture(path, "-hide_banner -nostdin " + arguments, null))
                  .Returns(output);
        }

        [Test]
        public void should_parse_encoder_names()
        {
            GivenOutput("-encoders",
                " Encoders:",
                " V..... = Video",
                " ------",
                " V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)",
                " V....D libx265              H.265 / HEVC (codec hevc)",
                " V....D av1_nvenc            NVIDIA NVENC av1 encoder (codec av1)");

            var encoders = Subject.GetEncoders();

            encoders.Should().Contain(new[] { "libx264", "libx265", "av1_nvenc" });
            encoders.Should().NotContain("Encoders:");
        }

        [Test]
        public void should_parse_filter_names()
        {
            GivenOutput("-filters",
                " Filters:",
                "  T.. = Timeline support",
                "  .S. = Slice threading",
                " ... abench            A->A       Benchmark part of a filtergraph.",
                " ... bilateral_cuda    V->V       GPU accelerated bilateral filter",
                " ... scale_vaapi       V->V       Scale to/from VAAPI surfaces.");

            var filters = Subject.GetFilters();

            filters.Should().Contain(new[] { "abench", "bilateral_cuda", "scale_vaapi" });
            filters.Should().NotContain("Filters:");
        }

        [Test]
        public void should_parse_ffmpeg8_two_char_filter_flags_and_ignore_the_legend()
        {
            // ffmpeg >= 8 shrinks the `-filters` flag column from 3 to 2 chars; legend rows still
            // match the pattern and would otherwise yield the "=" token.
            GivenOutput("-filters",
                " Filters:",
                "  T. = Timeline support",
                "  .S = Slice threading",
                "  ..C = Command support",
                "  A = Audio input/output",
                "  V = Video input/output",
                "  | = Source or sink filter",
                " .. abench            A->A       Benchmark part of a filtergraph.",
                " .. bilateral_cuda    V->V       GPU accelerated bilateral filter",
                " .. scale_vaapi       V->V       Scale to/from VAAPI surfaces.");

            var filters = Subject.GetFilters();

            filters.Should().Contain(new[] { "abench", "bilateral_cuda", "scale_vaapi" });
            filters.Should().NotContain("=");
            filters.Should().NotContain("Filters:");
        }

        [Test]
        public void should_return_empty_list_when_ffmpeg_is_missing()
        {
            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(provider => provider.GetFFmpegPath())
                  .Returns((string)null);

            Subject.GetEncoders().Should().BeEmpty();
        }

        [Test]
        public void should_cache_encoder_list_for_the_same_ffmpeg_path()
        {
            GivenOutput("-encoders", " V....D libx264              H.264");

            Subject.GetEncoders().Should().Contain("libx264");
            Subject.GetEncoders().Should().Contain("libx264");

            Mocker.GetMock<IProcessProvider>()
                  .Verify(provider => provider.StartAndCapture(FFmpegPath, "-hide_banner -nostdin -encoders", null), Times.Once);
        }

        [Test]
        public void should_rebuild_encoder_list_when_the_ffmpeg_path_changes()
        {
            const string OtherPath = "/opt/ffmpeg/bin/ffmpeg";

            GivenOutput("-encoders", " V....D libx264              H.264");
            Subject.GetEncoders().Should().Contain("libx264");

            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(provider => provider.GetFFmpegPath())
                  .Returns(OtherPath);

            GivenOutputForPath(OtherPath, "-encoders", " V....D libx265              H.265");

            Subject.GetEncoders().Should().Contain("libx265");
            Subject.GetEncoders().Should().NotContain("libx264");
        }

        [Test]
        public void should_rebuild_every_list_when_the_ffmpeg_path_changes()
        {
            const string OtherPath = "/opt/ffmpeg/bin/ffmpeg";

            GivenOutput("-encoders", " V....D libx264              H.264");
            GivenOutput("-decoders", " V....D h264                H.264");
            GivenOutput("-filters", " ... scale               V->V       Scale the input");

            Subject.GetEncoders().Should().Contain("libx264");
            Subject.GetDecoders().Should().Contain("h264");
            Subject.GetFilters().Should().Contain("scale");

            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(provider => provider.GetFFmpegPath())
                  .Returns(OtherPath);

            GivenOutputForPath(OtherPath, "-encoders", " V....D libx265              H.265");
            GivenOutputForPath(OtherPath, "-decoders", " V....D hevc                H.265");
            GivenOutputForPath(OtherPath, "-filters", " ... zscale              V->V       ZScale");

            Subject.GetEncoders().Should().Contain("libx265").And.NotContain("libx264");
            Subject.GetDecoders().Should().Contain("hevc").And.NotContain("h264");
            Subject.GetFilters().Should().Contain("zscale").And.NotContain("scale");
        }

        [Test]
        public void should_retry_when_the_first_probe_fails()
        {
            var calls = 0;

            Mocker.GetMock<IProcessProvider>()
                  .Setup(provider => provider.StartAndCapture(FFmpegPath, "-hide_banner -nostdin -encoders", null))
                  .Returns(() =>
                  {
                      if (calls++ == 0)
                      {
                          throw new InvalidOperationException("ffmpeg failed to start");
                      }

                      var output = new ProcessOutput { ExitCode = 0 };
                      output.Lines.Add(new ProcessOutputLine(ProcessOutputLevel.Standard, " V....D libx264              H.264"));

                      return output;
                  });

            Subject.GetEncoders().Should().BeEmpty();

            // The empty result of the failed probe was not cached, so the next call tries again.
            Subject.GetEncoders().Should().Contain("libx264");
        }
    }
}
