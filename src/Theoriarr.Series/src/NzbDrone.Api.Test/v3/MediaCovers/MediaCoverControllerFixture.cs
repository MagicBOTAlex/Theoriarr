using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.MediaCovers;

namespace NzbDrone.Api.Test.v3.MediaCovers
{
    [TestFixture]
    public class MediaCoverControllerFixture : TestBase<MediaCoverController>
    {
        private string _root;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(TempFolder, "appdata");
            Directory.CreateDirectory(_root);

            Mocker.GetMock<IAppFolderInfo>().SetupGet(f => f.AppDataFolder).Returns(_root);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(It.IsAny<string>()))
                  .Returns<string>(File.Exists);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileSize(It.IsAny<string>()))
                  .Returns<string>(path => new FileInfo(path).Length);
        }

        [Test]
        public void should_not_serve_files_outside_the_media_cover_root()
        {
            File.WriteAllText(Path.Combine(_root, "outside.jpg"), "secret");

            var result = Subject.GetMediaCover(1, "../../outside.jpg");

            result.Should().BeOfType<NotFoundResult>();
        }

        [Test]
        public void should_serve_files_inside_the_media_cover_root()
        {
            var coverFolder = Path.Combine(_root, "MediaCover", "1");
            Directory.CreateDirectory(coverFolder);
            File.WriteAllText(Path.Combine(coverFolder, "poster.jpg"), "image");

            var result = Subject.GetMediaCover(1, "poster.jpg");

            result.Should().BeOfType<PhysicalFileResult>();
        }
    }
}
