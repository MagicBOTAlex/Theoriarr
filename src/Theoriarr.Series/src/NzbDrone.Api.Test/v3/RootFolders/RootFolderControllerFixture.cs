using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.RootFolders;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.RootFolders
{
    [TestFixture]
    public class RootFolderControllerFixture : TestBase<RootFolderController>
    {
        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private void GivenUsage(Dictionary<int, string> seriesPaths, Dictionary<int, string> moviePaths)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(seriesPaths);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMoviePaths())
                  .Returns(moviePaths);
        }

        private static RootFolder Folder(int id, string path, MediaType mediaType)
        {
            return new RootFolder
            {
                Id = id,
                Path = path,
                MediaType = mediaType,
                UnmappedFolders = new List<UnmappedFolder>()
            };
        }

        [Test]
        public void get_all_should_fall_back_to_unfiltered_for_movie_key_ac2()
        {
            // AC2: the only root folder is shared and used by a series; the movie key must
            // still receive it.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, string> { { 1, "/media/tv" } },
                new Dictionary<int, string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder> { Folder(1, "/media/tv", MediaType.Series) });

            var result = Subject.GetRootFolders();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
        }

        [Test]
        public void get_all_should_omit_shared_series_only_folder_when_movie_folder_exists_ac5()
        {
            // AC5: a movie-visible alternative keeps the narrowing.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, string> { { 1, "/media/tv" } },
                new Dictionary<int, string> { { 2, "/media/movies" } });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder>
                  {
                      Folder(1, "/media/tv", MediaType.Series),
                      Folder(2, "/media/movies", MediaType.Series)
                  });

            var result = Subject.GetRootFolders();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(2);
        }

        [Test]
        public void get_by_id_should_return_shared_movie_only_folder_to_series_ac1()
        {
            // AC1: by-id read of a shared root folder referenced only by movies stays
            // available to the series key.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(
                new Dictionary<int, string>(),
                new Dictionary<int, string> { { 1, "/media/movies" } });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Get(3, It.IsAny<bool>()))
                  .Returns(Folder(3, "/media/movies", MediaType.Series));

            var result = Subject.GetResourceByIdWithErrorHandler(3);

            var ok = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<RootFolderResource>>().Subject;
            ok.Value.Should().NotBeNull();
            ok.Value.Id.Should().Be(3);
        }

        [Test]
        public void get_by_id_should_return_404_for_movie_only_folder_with_series_key()
        {
            // B3: hidden by-id read returns 404, never 200 with an empty body.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, string>(), new Dictionary<int, string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Get(9, It.IsAny<bool>()))
                  .Returns(Folder(9, "/media/movies", MediaType.Movie));

            Subject.Invoking(s => s.GetResourceByIdWithErrorHandler(9))
                   .Should().Throw<Sonarr.Http.REST.NotFoundException>()
                   .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public void create_should_stamp_media_type_from_movie_key_when_omitted()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(new Dictionary<int, string>(), new Dictionary<int, string> { { 12, "/media/movies" } });

            RootFolder added = null;
            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Add(It.IsAny<RootFolder>()))
                  .Callback<RootFolder>(f => added = f)
                  .Returns((RootFolder f) =>
                  {
                      f.Id = 12;
                      return f;
                  });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Get(12, It.IsAny<bool>()))
                  .Returns(() => added);

            Subject.CreateRootFolder(new RootFolderResource { Path = "/media/movies" });

            added.Should().NotBeNull();
            added.MediaType.Should().Be(MediaType.Movie);
        }

        [Test]
        public void create_should_honor_explicit_anime_media_type_from_series_key()
        {
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, string> { { 13, "/media/anime" } }, new Dictionary<int, string>());

            RootFolder added = null;
            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Add(It.IsAny<RootFolder>()))
                  .Callback<RootFolder>(f => added = f)
                  .Returns((RootFolder f) =>
                  {
                      f.Id = 13;
                      return f;
                  });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Get(13, It.IsAny<bool>()))
                  .Returns(() => added);

            Subject.CreateRootFolder(new RootFolderResource { Path = "/media/anime", MediaType = MediaType.Anime });

            added.Should().NotBeNull();
            added.MediaType.Should().Be(MediaType.Anime);
        }

        [Test]
        public void get_all_with_all_returns_every_folder_regardless_of_key()
        {
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, string>(), new Dictionary<int, string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder>
                  {
                      Folder(1, "/media/tv", MediaType.Series),
                      Folder(2, "/media/anime", MediaType.Anime),
                      Folder(3, "/media/movies", MediaType.Movie)
                  });

            var result = Subject.GetRootFolders(true);

            result.Should().HaveCount(3);
            result.Select(r => r.MediaType).Should().Equal(MediaType.Series, MediaType.Anime, MediaType.Movie);
        }

        [Test]
        public void get_all_should_hide_anime_folder_from_movie_key()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(new Dictionary<int, string> { { 1, "/media/anime" } }, new Dictionary<int, string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder>
                  {
                      Folder(1, "/media/anime", MediaType.Anime),
                      Folder(2, "/media/movies", MediaType.Movie)
                  });

            var result = Subject.GetRootFolders();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(2);
        }

        [Test]
        public void all_true_should_not_let_movie_key_enumerate_series_folders()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, string> { { 1, "/media/tv" } },
                new Dictionary<int, string> { { 2, "/media/movies" } });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder>
                  {
                      Folder(1, "/media/tv", MediaType.Series),
                      Folder(2, "/media/movies", MediaType.Movie)
                  });

            var result = Subject.GetRootFolders(true);

            result.Select(r => r.Id).Should().BeEquivalentTo(new[] { 2 });
        }

        [Test]
        public void all_true_should_still_let_series_key_enumerate_everything()
        {
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, string>(), new Dictionary<int, string>());

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.AllWithUnmappedFolders())
                  .Returns(new List<RootFolder>
                  {
                      Folder(1, "/media/tv", MediaType.Series),
                      Folder(2, "/media/movies", MediaType.Movie)
                  });

            var result = Subject.GetRootFolders(true);

            result.Should().HaveCount(2);
        }

        [Test]
        public void movie_key_create_should_reject_series_media_type()
        {
            GivenSubsystem(AppSubsystem.Movies);

            Subject.Invoking(s => s.CreateRootFolder(new RootFolderResource { Path = "/media/tv", MediaType = MediaType.Series }))
                   .Should().Throw<Sonarr.Http.REST.BadRequestException>();
        }

        [Test]
        public void series_key_create_should_reject_movie_media_type()
        {
            GivenSubsystem(AppSubsystem.Series);

            Subject.Invoking(s => s.CreateRootFolder(new RootFolderResource { Path = "/media/movies", MediaType = MediaType.Movie }))
                   .Should().Throw<Sonarr.Http.REST.BadRequestException>();
        }

        [Test]
        public void update_should_reject_cross_domain_media_type()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, string> { { 1, "/media/tv" } },
                new Dictionary<int, string> { { 1, "/media/tv" } });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.Get(1, It.IsAny<bool>()))
                  .Returns(Folder(1, "/media/tv", MediaType.Series));

            Subject.Invoking(s => s.UpdateRootFolder(new RootFolderResource { Id = 1, MediaType = MediaType.Series }))
                   .Should().Throw<Sonarr.Http.REST.BadRequestException>();
        }
    }
}
