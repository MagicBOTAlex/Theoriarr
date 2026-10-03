using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace Sonarr.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class MediaCoverMapperFixture : TestBase<MediaCoverMapper>
    {
        private string _appData;

        [SetUp]
        public void Setup()
        {
            _appData = Path.Combine(TempFolder, "appdata");

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(c => c.AppDataFolder)
                .Returns(_appData);
        }

        [Test]
        public void should_handle_series_media_cover()
        {
            Subject.CanHandle("/MediaCover/12/poster.jpg").Should().BeTrue();
            Subject.Map("/MediaCover/12/poster.jpg").Should().Be(Path.Combine(_appData, "MediaCover", "12", "poster.jpg"));
        }

        [Test]
        public void should_handle_movie_media_cover()
        {
            Subject.CanHandle("/MediaCover/movie/34/poster.jpg").Should().BeTrue();
            Subject.Map("/MediaCover/movie/34/poster.jpg").Should().Be(Path.Combine(_appData, "MediaCover", "movie", "34", "poster.jpg"));
        }

        [Test]
        public void should_not_handle_media_cover_proxy()
        {
            Subject.CanHandle("/MediaCoverProxy/hash/poster.jpg").Should().BeFalse();
        }
    }
}
