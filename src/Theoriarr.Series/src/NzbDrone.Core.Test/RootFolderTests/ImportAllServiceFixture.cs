using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.RootFolders.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.RootFolderTests
{
    [TestFixture]
    public class ImportAllServiceFixture : CoreTest<ImportAllService>
    {
        private RootFolder _rootFolder;

        [SetUp]
        public void Setup()
        {
            _rootFolder = new RootFolder
            {
                Path = "/tv".AsOsAgnostic(),
                MediaType = MediaType.Series,
                Accessible = true,
                UnmappedFolders = new List<UnmappedFolder>()
            };

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { new QualityProfile { Id = 1, Name = "Any" } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(new Dictionary<int, string>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new Dictionary<int, int>());

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMoviePaths())
                  .Returns(new Dictionary<int, string>());

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMovieTmdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder> { _rootFolder });
        }

        private void WithUnmappedFolder(string name, string path)
        {
            _rootFolder.UnmappedFolders.Add(new UnmappedFolder
            {
                Name = name,
                Path = path.AsOsAgnostic(),
                RelativePath = name
            });
        }

        [Test]
        public void should_add_unmapped_series_from_root_folder()
        {
            WithUnmappedFolder("Breaking Bad", "/tv/Breaking Bad");

            Mocker.GetMock<ISearchForNewSeries>()
                  .Setup(s => s.SearchForNewSeries("Breaking Bad"))
                  .Returns(new List<Series> { new Series { TvdbId = 81189, Title = "Breaking Bad", Year = 2008 } });

            Subject.Execute(new ImportAllCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(s => s.AddSeries(
                      It.Is<List<Series>>(l => l.Count == 1 &&
                                               l[0].TvdbId == 81189 &&
                                               l[0].Path == "/tv/Breaking Bad".AsOsAgnostic() &&
                                               l[0].RootFolderPath == "/tv".AsOsAgnostic() &&
                                               l[0].QualityProfileId == 1 &&
                                               l[0].Monitored),
                      true),
                  Times.Once());
        }

        [Test]
        public void should_add_unmapped_movie_from_root_folder()
        {
            _rootFolder.MediaType = MediaType.Movie;
            _rootFolder.Path = "/movies".AsOsAgnostic();
            WithUnmappedFolder("Inception (2010)", "/movies/Inception (2010)");

            Mocker.GetMock<ISearchForNewMovie>()
                  .Setup(s => s.SearchForNewMovie("Inception (2010)"))
                  .Returns(new List<Movie>
                  {
                      new Movie { MovieMetadata = new MovieMetadata { TmdbId = 27205, Title = "Inception", Year = 2010 } }
                  });

            Subject.Execute(new ImportAllCommand());

            Mocker.GetMock<IAddMovieService>()
                  .Verify(s => s.AddMovies(
                      It.Is<List<Movie>>(l => l.Count == 1 &&
                                              l[0].TmdbId == 27205 &&
                                              l[0].Path == "/movies/Inception (2010)".AsOsAgnostic() &&
                                              l[0].RootFolderPath == "/movies".AsOsAgnostic() &&
                                              l[0].QualityProfileId == 1 &&
                                              l[0].Monitored),
                      true),
                  Times.Once());
        }

        [Test]
        public void should_skip_unmapped_folder_that_is_already_mapped()
        {
            WithUnmappedFolder("Breaking Bad", "/tv/Breaking Bad");

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(new Dictionary<int, string> { { 1, "/tv/Breaking Bad".AsOsAgnostic() } });

            Subject.Execute(new ImportAllCommand());

            Mocker.GetMock<ISearchForNewSeries>()
                  .Verify(s => s.SearchForNewSeries(It.IsAny<string>()), Times.Never());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<List<Series>>(), true), Times.Never());
        }

        [Test]
        public void should_skip_series_that_is_already_in_library()
        {
            WithUnmappedFolder("Breaking Bad", "/tv/Breaking Bad");

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new Dictionary<int, int> { { 1, 81189 } });

            Mocker.GetMock<ISearchForNewSeries>()
                  .Setup(s => s.SearchForNewSeries("Breaking Bad"))
                  .Returns(new List<Series> { new Series { TvdbId = 81189, Title = "Breaking Bad", Year = 2008 } });

            Subject.Execute(new ImportAllCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<List<Series>>(), true), Times.Never());
        }

        [Test]
        public void should_not_add_when_lookup_fails()
        {
            WithUnmappedFolder("Breaking Bad", "/tv/Breaking Bad");

            Mocker.GetMock<ISearchForNewSeries>()
                  .Setup(s => s.SearchForNewSeries(It.IsAny<string>()))
                  .Throws(new Exception("lookup down"));

            Assert.DoesNotThrow(() => Subject.Execute(new ImportAllCommand()));

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<List<Series>>(), true), Times.Never());
        }

        [Test]
        public void should_not_add_when_no_match_is_found()
        {
            WithUnmappedFolder("Some Random Folder", "/tv/Some Random Folder");

            Mocker.GetMock<ISearchForNewSeries>()
                  .Setup(s => s.SearchForNewSeries(It.IsAny<string>()))
                  .Returns(new List<Series>());

            Subject.Execute(new ImportAllCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(s => s.AddSeries(It.IsAny<List<Series>>(), true), Times.Never());
        }
    }
}
