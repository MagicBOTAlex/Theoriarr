using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;

namespace NzbDrone.Api.Test.SignalR
{
    [TestFixture]
    public class ReleaseSearchProgressHandlerFixture : TestBase
    {
        private Mock<IBroadcastSignalRMessage> _broadcaster;
        private ReleaseSearchProgressHandler _subject;

        [SetUp]
        public void Setup()
        {
            _broadcaster = Mocker.GetMock<IBroadcastSignalRMessage>();
            _broadcaster.SetupGet(b => b.IsConnected).Returns(true);

            _subject = new ReleaseSearchProgressHandler(_broadcaster.Object);
        }

        [Test]
        public void should_broadcast_series_progress_to_the_series_hub()
        {
            _subject.HandleAsync(new ReleaseSearchProgressEvent
            {
                SearchId = "search-1",
                Domain = ReleaseSearchDomain.Series,
                Status = ReleaseSearchProgressStatus.Started,
                Indexers = new List<ReleaseSearchIndexerProgress>
                {
                    new ReleaseSearchIndexerProgress { Id = 3, Name = "Indexer" }
                }
            });

            _broadcaster.Verify(b => b.BroadcastMessage(It.Is<SignalRMessage>(m => m.Name == "releaseSearchProgress" && m.Version == 5 && m.Subsystem == SignalRSubsystem.Series)), Times.Once());
        }

        [Test]
        public void should_broadcast_movie_progress_to_the_movie_hub()
        {
            _subject.HandleAsync(new ReleaseSearchProgressEvent
            {
                SearchId = "search-1",
                Domain = ReleaseSearchDomain.Movies,
                Status = ReleaseSearchProgressStatus.IndexerCompleted,
                IndexerId = 4,
                IndexerName = "Indexer",
                ReleaseCount = 2
            });

            _broadcaster.Verify(b => b.BroadcastMessage(It.Is<SignalRMessage>(m => m.Name == "releaseSearchProgress" && m.Subsystem == SignalRSubsystem.Movies)), Times.Once());
        }

        [Test]
        public void should_not_broadcast_when_not_connected()
        {
            _broadcaster.SetupGet(b => b.IsConnected).Returns(false);

            _subject.HandleAsync(new ReleaseSearchProgressEvent
            {
                SearchId = "search-1",
                Domain = ReleaseSearchDomain.Series,
                Status = ReleaseSearchProgressStatus.Completed
            });

            _broadcaster.Verify(b => b.BroadcastMessage(It.IsAny<SignalRMessage>()), Times.Never());
        }
    }
}
