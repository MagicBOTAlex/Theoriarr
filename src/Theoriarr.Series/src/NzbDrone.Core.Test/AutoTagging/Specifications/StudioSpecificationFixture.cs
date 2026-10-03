using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.AutoTagging.Specifications
{
    [TestFixture]
    public class StudioSpecificationFixture : CoreTest<StudioSpecification>
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie
            {
                MovieMetadata = new MovieMetadata
                {
                    Studio = "Warner Bros. Pictures"
                }
            };
        }

        [Test]
        public void should_match_movie_with_matching_studio()
        {
            Subject.Value = new List<string> { "warner bros. pictures" };

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }

        [Test]
        public void should_not_match_movie_without_matching_studio()
        {
            Subject.Value = new List<string> { "universal pictures" };

            Subject.IsSatisfiedBy(_movie).Should().BeFalse();
        }

        [Test]
        public void should_match_negated_movie()
        {
            Subject.Value = new List<string> { "universal pictures" };
            Subject.Negate = true;

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }
    }
}
