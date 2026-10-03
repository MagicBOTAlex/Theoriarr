using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Test.Subsystem
{
    [TestFixture]
    public class SubsystemDomainScopeFixture
    {
        [Test]
        public void should_only_hide_series_only_rows_from_movies()
        {
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, usedBySeries: true, usedByMovies: false).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, usedBySeries: true, usedByMovies: false).Should().BeFalse();
        }

        [Test]
        public void should_always_show_shared_or_movie_only_rows_to_series()
        {
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, usedBySeries: false, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, usedBySeries: true, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, usedBySeries: false, usedByMovies: false).Should().BeTrue();

            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, usedBySeries: false, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, usedBySeries: true, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, usedBySeries: false, usedByMovies: false).Should().BeTrue();
        }

        [Test]
        public void should_always_show_shared_media_type_rows_to_series_ac1()
        {
            // AC1: a legacy/shared Series(0) row referenced only by movies must still be
            // visible to the series key.
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Series, usedBySeries: false, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Series, usedBySeries: true, usedByMovies: false).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Series, usedBySeries: true, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Series, usedBySeries: false, usedByMovies: false).Should().BeTrue();
        }

        [Test]
        public void should_keep_explicit_movie_rows_hidden_from_series_ac3()
        {
            // AC3: an explicit Movie row stays hidden from the series key.
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Movie, usedBySeries: false, usedByMovies: false).Should().BeFalse();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Movie, usedBySeries: true, usedByMovies: false).Should().BeFalse();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Series, MediaType.Movie, usedBySeries: false, usedByMovies: true).Should().BeFalse();
        }

        [Test]
        public void should_show_explicit_movie_rows_to_movies_ac4()
        {
            // AC4: an explicit Movie row is visible to the movie key.
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Movie, usedBySeries: false, usedByMovies: false).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Movie, usedBySeries: true, usedByMovies: false).Should().BeTrue();
        }

        [Test]
        public void should_omit_shared_series_only_rows_from_movies_ac5()
        {
            // AC5: while a movie-visible alternative exists the movie key narrows shared
            // rows referenced only by series.
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Series, usedBySeries: true, usedByMovies: false).Should().BeFalse();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Series, usedBySeries: true, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Series, usedBySeries: false, usedByMovies: true).Should().BeTrue();
            SubsystemDomainScope.IsVisibleTo(AppSubsystem.Movies, MediaType.Series, usedBySeries: false, usedByMovies: false).Should().BeTrue();
        }

        [Test]
        public void should_fall_back_to_unfiltered_rows_for_movies_when_filtered_is_empty_ac2()
        {
            // AC2: the only shared row is referenced by series, so the filtered movie list
            // is empty and the unfiltered list is returned to keep setup possible.
            var all = new[] { "shared-series-only" };
            var filtered = Array.Empty<string>();

            SubsystemDomainScope.WithSetupFallback(AppSubsystem.Movies, all, filtered).Should().Equal(all);
        }

        [Test]
        public void should_not_fall_back_for_movies_when_a_movie_visible_row_exists_ac5()
        {
            // AC5: a movie-visible alternative keeps the narrowing.
            var all = new[] { "shared-series-only", "movie-visible" };
            var filtered = new[] { "movie-visible" };

            SubsystemDomainScope.WithSetupFallback(AppSubsystem.Movies, all, filtered).Should().Equal(filtered);
        }

        [Test]
        public void should_never_fall_back_for_series()
        {
            // The series key is never narrowed to empty by IsVisibleTo; explicit Movie rows
            // stay hidden even when nothing else is visible.
            var all = new[] { "movie" };
            var filtered = Array.Empty<string>();

            SubsystemDomainScope.WithSetupFallback(AppSubsystem.Series, all, filtered).Should().BeEmpty();
        }

        [Test]
        public void should_map_subsystem_to_media_type()
        {
            AppSubsystem.Series.ToMediaType().Should().Be(MediaType.Series);
            AppSubsystem.Movies.ToMediaType().Should().Be(MediaType.Movie);
        }

        [Test]
        public void should_detect_paths_under_a_root_folder()
        {
            var seriesPaths = new[] { "/media/tv/Show A", "/media/tv/Show B" };

            "/media/tv".IsPathUnder(seriesPaths).Should().BeTrue();
            "/media/tv/Show A".IsPathUnder(seriesPaths).Should().BeTrue();
            "/media/movies".IsPathUnder(seriesPaths).Should().BeFalse();
            "/media/tv2".IsPathUnder(seriesPaths).Should().BeFalse();
            ((string)null).IsPathUnder(seriesPaths).Should().BeFalse();
        }
    }
}
