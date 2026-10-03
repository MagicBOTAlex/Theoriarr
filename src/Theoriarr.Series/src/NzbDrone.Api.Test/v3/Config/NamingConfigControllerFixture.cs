using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Organizer;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Config;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Config
{
    [TestFixture]
    public class NamingConfigControllerFixture : TestBase<NamingConfigController>
    {
        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private void GivenStored(NamingConfig stored)
        {
            Mocker.GetMock<INamingConfigService>()
                  .Setup(s => s.GetConfig())
                  .Returns(stored);
        }

        [Test]
        public void movie_update_should_not_clobber_series_formats()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenStored(new NamingConfig
            {
                Id = 1,
                RenameEpisodes = true,
                StandardEpisodeFormat = "series-format",
                DailyEpisodeFormat = "daily-format",
                AnimeEpisodeFormat = "anime-format",
                SeriesFolderFormat = "series-folder",
                SeasonFolderFormat = "season-folder",
                SpecialsFolderFormat = "specials-folder",
                MultiEpisodeStyle = MultiEpisodeStyle.PrefixedRange,
                StandardMovieFormat = "old-movie-format",
                MovieFolderFormat = "old-movie-folder"
            });

            NamingConfig saved = null;
            Mocker.GetMock<INamingConfigService>()
                  .Setup(s => s.Save(It.IsAny<NamingConfig>()))
                  .Callback<NamingConfig>(c => saved = c);

            // A partial Radarr-shaped payload that omits every series field.
            Subject.UpdateNamingConfig(new NamingConfigResource
            {
                Id = 1,
                RenameMovies = true,
                StandardMovieFormat = "new-movie-format",
                MovieFolderFormat = "new-movie-folder",
                ReplaceIllegalCharacters = true
            });

            saved.Should().NotBeNull();
            saved.RenameMovies.Should().BeTrue();
            saved.StandardMovieFormat.Should().Be("new-movie-format");
            saved.MovieFolderFormat.Should().Be("new-movie-folder");

            saved.RenameEpisodes.Should().BeTrue();
            saved.StandardEpisodeFormat.Should().Be("series-format");
            saved.DailyEpisodeFormat.Should().Be("daily-format");
            saved.AnimeEpisodeFormat.Should().Be("anime-format");
            saved.SeriesFolderFormat.Should().Be("series-folder");
            saved.SeasonFolderFormat.Should().Be("season-folder");
            saved.SpecialsFolderFormat.Should().Be("specials-folder");
        }

        [Test]
        public void series_update_should_not_clobber_movie_formats()
        {
            GivenSubsystem(AppSubsystem.Series);
            GivenStored(new NamingConfig
            {
                Id = 1,
                StandardMovieFormat = "movie-format",
                MovieFolderFormat = "movie-folder",
                RenameMovies = true,
                StandardEpisodeFormat = "old-series-format"
            });

            NamingConfig saved = null;
            Mocker.GetMock<INamingConfigService>()
                  .Setup(s => s.Save(It.IsAny<NamingConfig>()))
                  .Callback<NamingConfig>(c => saved = c);

            Subject.UpdateNamingConfig(new NamingConfigResource
            {
                Id = 1,
                StandardEpisodeFormat = "new-series-format",
                ReplaceIllegalCharacters = true
            });

            saved.Should().NotBeNull();
            saved.StandardEpisodeFormat.Should().Be("new-series-format");
            saved.StandardMovieFormat.Should().Be("movie-format");
            saved.MovieFolderFormat.Should().Be("movie-folder");
            saved.RenameMovies.Should().BeTrue();
        }
    }
}
