using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging.Specifications;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Test.AutoTagging.Specifications
{
    [TestFixture]
    public class SeriesSpecificationMovieFixture
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie
            {
                MovieMetadata = new MovieMetadata
                {
                    Genres = new List<string> { "Comedy" }
                }
            };
        }

        [Test]
        public void series_only_specification_should_not_match_movie()
        {
            var specification = new GenreSpecification
            {
                Value = new List<string> { "Comedy" }
            };

            specification.IsSatisfiedBy(_movie).Should().BeFalse();
        }

        [Test]
        public void series_only_specification_should_match_negated_movie()
        {
            var specification = new GenreSpecification
            {
                Value = new List<string> { "Comedy" },
                Negate = true
            };

            specification.IsSatisfiedBy(_movie).Should().BeTrue();
        }
    }
}
