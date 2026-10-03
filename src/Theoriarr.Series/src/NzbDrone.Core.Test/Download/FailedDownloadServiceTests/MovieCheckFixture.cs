using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.FailedDownloadServiceTests
{
    [TestFixture]
    public class MovieCheckFixture : CoreTest<FailedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            _trackedDownload = new TrackedDownload
            {
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem
                {
                    Status = DownloadItemStatus.Failed,
                    Title = "Some.Movie.2020"
                },
                RemoteMovie = new RemoteMovie()
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindMovie(It.IsAny<string>(), MovieHistoryEventType.Grabbed))
                .Returns(new List<MovieHistory>());
        }

        [Test]
        public void should_warn_with_app_name_for_movie_download_not_grabbed()
        {
            Subject.Check(_trackedDownload);

            _trackedDownload.StatusMessages.Should().ContainSingle();
            _trackedDownload.StatusMessages[0].Messages.Should().ContainSingle()
                .Which.Should().Contain(BuildInfo.AppName)
                .And.NotContain("Radarr");
        }
    }
}
