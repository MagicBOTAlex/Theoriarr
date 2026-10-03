using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using Sonarr.Api.V3.Movies;

namespace NzbDrone.Api.Test.v3.Movies
{
    [TestFixture]
    public class MovieResourceFixture
    {
        [Test]
        public void should_expose_the_internal_movie_id_as_title_slug()
        {
            // Jellyseerr stores the slug for its "open in Radarr" link; the unified SPA
            // resolves /movie/:id, so the slug must be the internal id, not the TMDB id.
            var movie = new Movie
            {
                Id = 42,
                Path = "/media/movies/Example",
                QualityProfileId = 1,
                MovieMetadata = new MovieMetadata
                {
                    TmdbId = 550,
                    Title = "Example",
                    OriginalTitle = "Example",
                    CleanTitle = "example"
                }
            };

            var resource = movie.ToResource(0);

            resource.Id.Should().Be(42);
            resource.TmdbId.Should().Be(550);
            resource.TitleSlug.Should().Be("42");
        }

        [Test]
        public void should_expose_the_tmdb_id_as_title_slug_for_lookup_rows()
        {
            // A lookup result that has not been added yet has Id == 0. Upstream Radarr uses the
            // TMDB id here, and a slug of "0" would be meaningless to API consumers.
            var movie = new Movie
            {
                Id = 0,
                Path = "/media/movies/Example",
                QualityProfileId = 1,
                MovieMetadata = new MovieMetadata
                {
                    TmdbId = 550,
                    Title = "Example",
                    OriginalTitle = "Example",
                    CleanTitle = "example"
                }
            };

            var resource = movie.ToResource(0);

            resource.Id.Should().Be(0);
            resource.TmdbId.Should().Be(550);
            resource.TitleSlug.Should().Be("550");
        }

        [Test]
        public void partial_update_should_preserve_path_root_folder_and_add_options()
        {
            // An external client may PUT only some fields (e.g. { id, monitored }); the relaxed
            // PUT validator accepts it, so the merge must not wipe the stored values.
            var existing = new Movie
            {
                Id = 1,
                Path = "/media/movies/Example",
                RootFolderPath = "/media/movies",
                QualityProfileId = 1,
                Monitored = true,
                AddOptions = new AddMovieOptions { SearchForMovie = true }
            };

            var resource = new MovieResource
            {
                Id = 1,
                Monitored = false
            };

            var updated = resource.ToModel(existing);

            updated.Path.Should().Be("/media/movies/Example");
            updated.RootFolderPath.Should().Be("/media/movies");
            updated.AddOptions.Should().NotBeNull();
            updated.AddOptions.SearchForMovie.Should().BeTrue();
            updated.Monitored.Should().BeFalse();
        }

        [Test]
        public void full_update_should_replace_path_root_folder_and_add_options()
        {
            var existing = new Movie
            {
                Id = 1,
                Path = "/media/movies/Example",
                RootFolderPath = "/media/movies",
                QualityProfileId = 1,
                AddOptions = new AddMovieOptions { SearchForMovie = true }
            };

            var resource = new MovieResource
            {
                Id = 1,
                Path = "/media/movies/Other",
                RootFolderPath = "/media/other",
                QualityProfileId = 2,
                AddOptions = new AddMovieOptions { SearchForMovie = false }
            };

            var updated = resource.ToModel(existing);

            updated.Path.Should().Be("/media/movies/Other");
            updated.RootFolderPath.Should().Be("/media/other");
            updated.QualityProfileId.Should().Be(2);
            updated.AddOptions.SearchForMovie.Should().BeFalse();
        }
    }
}
