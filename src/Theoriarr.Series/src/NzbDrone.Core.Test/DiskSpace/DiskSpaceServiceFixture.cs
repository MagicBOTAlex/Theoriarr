using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.DiskSpace;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DiskSpace
{
    [TestFixture]
    public class DiskSpaceServiceFixture : CoreTest<DiskSpaceService>
    {
        private string _seriesFolder;
        private string _seriesFolder2;
        private string _movieFolder;
        private string _rootFolder;
        private string _rootFolder2;

        [SetUp]
        public void SetUp()
        {
            _seriesFolder = @"G:\fasdlfsdf\series".AsOsAgnostic();
            _seriesFolder2 = @"G:\fasdlfsdf\series2".AsOsAgnostic();
            _movieFolder = @"H:\movies\movie".AsOsAgnostic();
            _rootFolder = @"G:\fasdlfsdf".AsOsAgnostic();
            _rootFolder2 = @"H:\movies".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetMounts())
                  .Returns(new List<IMount>());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetPathRoot(It.IsAny<string>()))
                  .Returns(@"G:\".AsOsAgnostic());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetAvailableSpace(It.IsAny<string>()))
                  .Returns(0);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetTotalSize(It.IsAny<string>()))
                  .Returns(0);

            GivenSeries();
            GivenMovies();
            GivenRootFolders();
        }

        private void GivenSeries(params string[] seriesPaths)
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetAllSeriesPaths())
                .Returns(new Dictionary<int, string>(seriesPaths.Select((value, i) => new KeyValuePair<int, string>(i, value))));
        }

        private void GivenMovies(params string[] moviePaths)
        {
            Mocker.GetMock<IMovieService>()
                .Setup(v => v.AllMoviePaths())
                .Returns(new Dictionary<int, string>(moviePaths.Select((value, i) => new KeyValuePair<int, string>(i, value))));
        }

        private void GivenRootFolder(string seriesPath, string rootFolderPath)
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(v => v.GetBestRootFolderPath(seriesPath))
                .Returns(rootFolderPath);
        }

        private void GivenRootFolders(params string[] rootFolderPaths)
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(v => v.All())
                .Returns(rootFolderPaths.Select(p => new RootFolder { Path = p }).ToList());
        }

        private void GivenExistingFolder(string folder)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FolderExists(folder))
                  .Returns(true);
        }

        [Test]
        public void should_check_diskspace_for_series_folders()
        {
            GivenSeries(_seriesFolder);
            GivenRootFolder(_seriesFolder, _rootFolder);
            GivenExistingFolder(_rootFolder);

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().NotBeEmpty();
        }

        [Test]
        public void should_check_diskspace_for_movie_folders()
        {
            GivenMovies(_movieFolder);
            GivenRootFolder(_movieFolder, _rootFolder2);
            GivenExistingFolder(_rootFolder2);

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().NotBeEmpty();
        }

        [Test]
        public void should_check_diskspace_for_same_root_folder_only_once_across_series_and_movies()
        {
            GivenSeries(_seriesFolder);
            GivenMovies(_movieFolder);
            GivenRootFolder(_seriesFolder, _rootFolder);
            GivenRootFolder(_movieFolder, _rootFolder);
            GivenExistingFolder(_rootFolder);

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().HaveCount(1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetAvailableSpace(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_check_diskspace_for_same_root_folder_only_once()
        {
            GivenSeries(_seriesFolder, _seriesFolder2);
            GivenRootFolder(_seriesFolder, _rootFolder);
            GivenRootFolder(_seriesFolder2, _rootFolder);
            GivenExistingFolder(_rootFolder);

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().HaveCount(1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetAvailableSpace(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_not_check_diskspace_for_missing_series_root_folders()
        {
            GivenSeries(_seriesFolder);
            GivenRootFolder(_seriesFolder, _rootFolder);

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().BeEmpty();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetAvailableSpace(It.IsAny<string>()), Times.Never());
        }

        [TestCase("/boot")]
        [TestCase("/var/lib/rancher")]
        [TestCase("/var/lib/rancher/volumes")]
        [TestCase("/var/lib/kubelet")]
        [TestCase("/var/lib/docker")]
        [TestCase("/some/place/docker/aufs")]
        [TestCase("/etc/network")]
        [TestCase("/Volumes/.timemachine/ABC123456-A1BC-12A3B45678C9/2025-05-13-181401.backup")]
        public void should_not_check_diskspace_for_irrelevant_mounts(string path)
        {
            var mount = new Mock<IMount>();
            mount.SetupGet(v => v.RootDirectory).Returns(path);
            mount.SetupGet(v => v.DriveType).Returns(System.IO.DriveType.Fixed);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetMounts())
                  .Returns(new List<IMount> { mount.Object });

            var freeSpace = Subject.GetFreeSpace();

            freeSpace.Should().BeEmpty();
        }

        [Test]
        public void should_return_no_content_when_folder_does_not_exist()
        {
            var content = Subject.GetContent(@"Z:\missing".AsOsAgnostic());

            content.Should().BeEmpty();
        }

        [Test]
        public void should_list_folder_contents_sorted_by_size()
        {
            var root = @"G:\downloads".AsOsAgnostic();
            var folder = @"G:\downloads\Some.Folder".AsOsAgnostic();
            var file = @"G:\downloads\movie.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(v => v.FolderExists(root)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetDirectories(root)).Returns(new[] { folder });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFiles(root, false)).Returns(new[] { file });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFolderSize(folder)).Returns(5_000_000_000L);
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFileSize(file)).Returns(1_000_000L);

            var content = Subject.GetContent(root);

            content.Should().HaveCount(2);

            content[0].Name.Should().Be("Some.Folder");
            content[0].IsFile.Should().BeFalse();
            content[0].Size.Should().Be(5_000_000_000L);

            content[1].Name.Should().Be("movie.mkv");
            content[1].IsFile.Should().BeTrue();
            content[1].Size.Should().Be(1_000_000L);
        }

        [Test]
        public void should_exclude_media_root_folders_and_their_contents()
        {
            var root = @"G:\data".AsOsAgnostic();
            var media = @"G:\data\media".AsOsAgnostic();
            var seriesRoot = @"G:\data\media\tv".AsOsAgnostic();
            var downloads = @"G:\data\downloads".AsOsAgnostic();

            GivenRootFolders(seriesRoot);

            Mocker.GetMock<IDiskProvider>().Setup(v => v.FolderExists(root)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetDirectories(root)).Returns(new[] { media, downloads });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFiles(root, false)).Returns(Array.Empty<string>());
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFolderSize(downloads)).Returns(5_000_000_000L);

            var content = Subject.GetContent(root);

            content.Should().HaveCount(1);
            content.Single().Name.Should().Be("downloads");
        }

        [Test]
        public void should_exclude_system_directories()
        {
            var root = @"G:\".AsOsAgnostic();
            var downloads = @"G:\downloads".AsOsAgnostic();
            var proc = @"G:\proc".AsOsAgnostic();
            var sys = @"G:\sys".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(v => v.FolderExists(root)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetDirectories(root)).Returns(new[] { downloads, proc, sys });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFiles(root, false)).Returns(Array.Empty<string>());
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFolderSize(downloads)).Returns(1L);

            var content = Subject.GetContent(root);

            content.Should().HaveCount(1);
            content.Single().Name.Should().Be("downloads");
        }

        [Test]
        public void should_skip_entries_whose_size_cannot_be_determined()
        {
            var root = @"G:\downloads".AsOsAgnostic();
            var badFolder = @"G:\downloads\Locked".AsOsAgnostic();
            var goodFile = @"G:\downloads\movie.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(v => v.FolderExists(root)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetDirectories(root)).Returns(new[] { badFolder });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFiles(root, false)).Returns(new[] { goodFile });
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFolderSize(badFolder)).Throws(new UnauthorizedAccessException());
            Mocker.GetMock<IDiskProvider>().Setup(v => v.GetFileSize(goodFile)).Returns(10L);

            var content = Subject.GetContent(root);

            content.Should().HaveCount(1);
            content.Single().Name.Should().Be("movie.mkv");
        }
    }
}
