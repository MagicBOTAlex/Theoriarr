using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeProgressParserFixture
    {
        [Test]
        public void should_compute_percent_and_speed()
        {
            var parser = new TranscodeProgressParser(100);

            parser.Parse("out_time_us=25000000").Should().BeNull();
            parser.Parse("speed=1.5x").Should().BeNull();
            parser.Parse("fps=45.6").Should().BeNull();
            parser.Parse("total_size=1048576").Should().BeNull();

            var progress = parser.Parse("progress=continue");

            progress.Should().NotBeNull();
            progress.Percent.Should().BeApproximately(25, 0.01);
            progress.Speed.Should().Be("1.5x");
            progress.Fps.Should().Be("45.6");
            progress.OutputSize.Should().Be(1048576);
            progress.Completed.Should().BeFalse();
            progress.Eta.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void should_flag_completion_on_progress_end()
        {
            var parser = new TranscodeProgressParser(100);

            parser.Parse("out_time_us=100000000");

            var progress = parser.Parse("progress=end");

            progress.Completed.Should().BeTrue();
            progress.Percent.Should().Be(100);
        }

        [Test]
        public void should_ignore_unparseable_lines()
        {
            var parser = new TranscodeProgressParser(100);

            parser.Parse("frame=123").Should().BeNull();
            parser.Parse("").Should().BeNull();
            parser.Parse("no separator here").Should().BeNull();
        }

        [Test]
        public void should_report_zero_percent_when_duration_is_unknown()
        {
            var parser = new TranscodeProgressParser(0);

            parser.Parse("out_time_us=5000000");

            var progress = parser.Parse("progress=continue");

            progress.Percent.Should().Be(0);
        }
    }
}
