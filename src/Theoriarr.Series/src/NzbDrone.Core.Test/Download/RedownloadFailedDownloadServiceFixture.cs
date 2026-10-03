using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class RedownloadFailedDownloadServiceFixture : CoreTest<RedownloadFailedDownloadService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(c => c.AutoRedownloadFailed)
                  .Returns(true);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(c => c.AutoRedownloadFailedFromInteractiveSearch)
                  .Returns(true);
        }

        [Test]
        public void should_search_for_a_failed_movie()
        {
            Subject.Handle(new DownloadFailedEvent
            {
                MovieId = 5,
                SourceTitle = "A Movie 1998"
            });

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<MoviesSearchCommand>(s => s.MovieIds.Contains(5)),
                                      It.IsAny<CommandPriority>(),
                                      It.IsAny<CommandTrigger>()),
                          Times.Once());
        }

        [Test]
        public void should_not_throw_for_a_failed_movie_without_episodes()
        {
            var message = new DownloadFailedEvent { MovieId = 5 };

            Assert.DoesNotThrow(() => Subject.Handle(message));
        }

        [Test]
        public void should_not_redownload_when_disabled()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(c => c.AutoRedownloadFailed)
                  .Returns(false);

            Subject.Handle(new DownloadFailedEvent { MovieId = 5 });

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<MoviesSearchCommand>(),
                                      It.IsAny<CommandPriority>(),
                                      It.IsAny<CommandTrigger>()),
                          Times.Never());
        }
    }
}
