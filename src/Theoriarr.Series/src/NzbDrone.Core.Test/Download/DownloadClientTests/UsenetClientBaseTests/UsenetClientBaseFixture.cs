using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.UsenetClientBaseTests
{
    [TestFixture]
    public class UsenetClientBaseFixture : DownloadClientFixtureBase<TestUsenetClient>
    {
        [Test]
        public async Task should_download_episode_from_nzb()
        {
            var remoteEpisode = CreateRemoteEpisode();

            var id = await Subject.Download(remoteEpisode, CreateIndexer());

            id.Should().NotBeNullOrEmpty();
            Subject.EpisodeNzbAdded.Should().BeTrue();
            Subject.MovieNzbAdded.Should().BeFalse();
        }

        [Test]
        public async Task should_download_movie_from_nzb()
        {
            var remoteMovie = CreateRemoteMovie();

            var id = await Subject.Download(remoteMovie, CreateIndexer());

            id.Should().NotBeNullOrEmpty();
            Subject.MovieNzbAdded.Should().BeTrue();
            Subject.EpisodeNzbAdded.Should().BeFalse();

            Mocker.GetMock<IValidateNzbs>()
                  .Verify(v => v.Validate(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Once());
        }
    }
}
