using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Library;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Library
{
    [TestFixture]
    public class LibrarySearchServiceFixture : CoreTest<LibrarySearchService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series>());

            Mocker.GetMock<IMovieService>()
                .Setup(s => s.GetAllMovies())
                .Returns(new List<Movie>());

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>());
        }

        private void GivenSeries(params Series[] series)
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(series.ToList());
        }

        private void GivenMovies(params Movie[] movies)
        {
            Mocker.GetMock<IMovieService>()
                .Setup(s => s.GetAllMovies())
                .Returns(movies.ToList());
        }

        [Test]
        public void should_match_series_by_substring_title()
        {
            GivenSeries(new Series { Id = 1, Title = "Smoking Gun", SortTitle = "smoking gun", Year = 2020 });

            var result = Subject.Search("smoki");

            result.Should().ContainSingle();
            result[0].MediaType.Should().Be(MediaType.Series);
            result[0].Id.Should().Be(1);
        }

        [Test]
        public void should_match_movie_by_substring_title()
        {
            GivenMovies(new Movie
            {
                Id = 5,
                MovieMetadata = new MovieMetadata { Title = "Smokin Aces", SortTitle = "smokin aces", Year = 2006 }
            });

            var result = Subject.Search("smoki");

            result.Should().ContainSingle();
            result[0].MediaType.Should().Be(MediaType.Movie);
            result[0].Id.Should().Be(5);
        }

        [Test]
        public void should_match_series_by_overview()
        {
            GivenSeries(new Series
            {
                Id = 1,
                Title = "Breaking Bad",
                SortTitle = "breaking bad",
                Overview = "A chemistry teacher starts smoking meth.",
                Year = 2008
            });

            Subject.Search("smoking").Should().ContainSingle();
        }

        [Test]
        public void should_match_series_by_alternate_title()
        {
            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(123))
                .Returns(new List<SceneMapping> { new SceneMapping { Title = "Alternate Smoking Name" } });

            GivenSeries(new Series { Id = 1, TvdbId = 123, Title = "Whatever", SortTitle = "whatever", Year = 2000 });

            Subject.Search("smoki").Should().ContainSingle();
        }

        [Test]
        public void should_match_movie_by_original_title()
        {
            GivenMovies(new Movie
            {
                Id = 5,
                MovieMetadata = new MovieMetadata { Title = "Whatever", SortTitle = "whatever", OriginalTitle = "Smokin Aces", Year = 2006 }
            });

            Subject.Search("smoki").Should().ContainSingle();
        }

        [Test]
        public void should_be_case_and_diacritic_insensitive()
        {
            GivenSeries(new Series { Id = 1, Title = "Café", SortTitle = "cafe", Year = 2000 });

            Subject.Search("cafe").Should().ContainSingle();
        }

        [Test]
        public void should_not_match_when_not_a_substring()
        {
            GivenSeries(new Series { Id = 1, Title = "Smiling Friends", SortTitle = "smiling friends", Year = 2022 });

            Subject.Search("smokin").Should().BeEmpty();
        }

        [Test]
        public void should_rank_title_match_before_overview_match()
        {
            GivenSeries(
                new Series { Id = 1, Title = "Alpha", SortTitle = "alpha", Overview = "smoking gun", Year = 2000 },
                new Series { Id = 2, Title = "Smoking Gun", SortTitle = "smoking gun", Year = 2000 });

            var result = Subject.Search("smoki");

            result.Should().HaveCount(2);
            result[0].Id.Should().Be(2);
        }

        [Test]
        public void should_return_empty_for_blank_term()
        {
            GivenSeries(new Series { Id = 1, Title = "Smoking Gun", SortTitle = "smoking gun", Year = 2000 });

            Subject.Search("   ").Should().BeEmpty();
        }
    }
}
