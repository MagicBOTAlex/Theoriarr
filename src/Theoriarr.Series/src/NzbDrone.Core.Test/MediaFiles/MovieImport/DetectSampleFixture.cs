using System;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport
{
    [TestFixture]
    public class DetectSampleFixture : CoreTest<DetectSample>
    {
        private MovieMetadata _movie;
        private string _path;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<MovieMetadata>.CreateNew()
                                           .With(m => m.Runtime = 120)
                                           .Build();

            _path = "/test/movie.mkv";
        }

        private void GivenRuntime(int seconds)
        {
            var mediaInfo = Builder<MediaInfoModel>.CreateNew()
                                                   .With(m => m.RunTime = new TimeSpan(0, 0, seconds))
                                                   .Build();

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(s => s.ProbeMediaInfo(It.IsAny<string>()))
                  .Returns(new MediaInfoProbeResult { MediaInfo = mediaInfo });
        }

        [Test]
        public void should_return_not_a_sample_when_runtime_is_long_enough()
        {
            GivenRuntime(600);

            Subject.IsSample(_movie, _path).Should().Be(DetectSampleResult.NotSample);
        }

        [Test]
        public void should_return_a_sample_when_runtime_is_too_short()
        {
            GivenRuntime(60);

            Subject.IsSample(_movie, _path).Should().Be(DetectSampleResult.Sample);
        }

        [Test]
        public void should_skip_probe_for_flv()
        {
            _path = "/test/movie.flv";

            Subject.IsSample(_movie, _path).Should().Be(DetectSampleResult.NotSample);

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.ProbeMediaInfo(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_probe_error_as_indeterminate_reason()
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(s => s.ProbeMediaInfo(It.IsAny<string>()))
                  .Returns(new MediaInfoProbeResult { Error = "invalid data found when processing input" });

            Subject.IsSample(_movie, _path, out var reason).Should().Be(DetectSampleResult.Indeterminate);

            reason.Should().Contain("invalid data found when processing input");

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
