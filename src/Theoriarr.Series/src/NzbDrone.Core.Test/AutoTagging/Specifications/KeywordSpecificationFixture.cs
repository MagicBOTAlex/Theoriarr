using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.AutoTagging.Specifications
{
    [TestFixture]
    public class KeywordSpecificationFixture : CoreTest<KeywordSpecification>
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie
            {
                MovieMetadata = new MovieMetadata
                {
                    Keywords = new List<string> { "Action", "Thriller" }
                }
            };
        }

        [Test]
        public void should_match_movie_with_matching_keyword()
        {
            Subject.Value = new List<string> { "action" };

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }

        [Test]
        public void should_not_match_movie_without_matching_keyword()
        {
            Subject.Value = new List<string> { "comedy" };

            Subject.IsSatisfiedBy(_movie).Should().BeFalse();
        }

        [Test]
        public void should_match_negated_movie()
        {
            Subject.Value = new List<string> { "comedy" };
            Subject.Negate = true;

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }
    }
}
