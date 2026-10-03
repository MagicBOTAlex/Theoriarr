using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class FFmpegMissingFixture : CoreTest<FFmpegMissingHealthCheck>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("Some Error Message");

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                  .Returns("Some Error Message");
        }

        [Test]
        public void should_return_error_when_no_ffmpeg_is_available()
        {
            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(s => s.GetFFmpegPath())
                  .Returns((string)null);

            Subject.Check().ShouldBeError();
        }

        [Test]
        public void should_return_error_when_ffmpeg_cannot_be_executed()
        {
            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(s => s.GetFFmpegPath())
                  .Returns("/usr/bin/ffmpeg");

            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(s => s.GetVersion(It.IsAny<string>()))
                  .Returns((string)null);

            Subject.Check().ShouldBeError();
        }

        [Test]
        public void should_return_ok_when_ffmpeg_is_available()
        {
            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(s => s.GetFFmpegPath())
                  .Returns("/usr/bin/ffmpeg");

            Mocker.GetMock<IFFmpegProvider>()
                  .Setup(s => s.GetVersion(It.IsAny<string>()))
                  .Returns("6.1.1");

            Subject.Check().ShouldBeOk();
        }
    }
}
