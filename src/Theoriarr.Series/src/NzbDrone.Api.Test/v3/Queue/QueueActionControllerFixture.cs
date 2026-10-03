using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Queue;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class QueueActionControllerFixture : TestBase<QueueActionController>
    {
        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private static NzbDrone.Core.Queue.Queue MovieItem()
        {
            return new NzbDrone.Core.Queue.Queue { Id = 2, RemoteMovie = new RemoteMovie() };
        }

        private static NzbDrone.Core.Queue.Queue SeriesItem()
        {
            return new NzbDrone.Core.Queue.Queue { Id = 1, RemoteEpisode = new RemoteEpisode() };
        }

        [Test]
        public async Task grab_should_reject_a_movie_item_for_the_series_key()
        {
            GivenSubsystem(AppSubsystem.Series);
            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.FindPendingQueueItem(2))
                  .Returns(MovieItem());

            await FluentActions.Awaiting(() => Subject.Grab(2))
                               .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task grab_should_still_accept_a_series_item()
        {
            GivenSubsystem(AppSubsystem.Series);
            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.FindPendingQueueItem(1))
                  .Returns(SeriesItem());
            Mocker.GetMock<IDownloadService>()
                  .Setup(s => s.DownloadReport(It.IsAny<RemoteEpisode>(), It.IsAny<int?>()))
                  .Returns(Task.CompletedTask);

            await Subject.Grab(1);
        }

        [Test]
        public void fixpath_should_reject_a_movie_item_for_the_series_key()
        {
            GivenSubsystem(AppSubsystem.Series);
            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.Find(2))
                  .Returns(MovieItem());

            Subject.Invoking(s => s.FixPath(2))
                   .Should().Throw<NotFoundException>();
        }
    }
}
