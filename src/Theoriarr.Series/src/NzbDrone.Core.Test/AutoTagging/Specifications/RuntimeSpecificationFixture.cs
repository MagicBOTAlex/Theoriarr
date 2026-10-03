using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.AutoTagging.Specifications
{
    [TestFixture]
    public class RuntimeSpecificationFixture : CoreTest<RuntimeSpecification>
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie
            {
                MovieMetadata = new MovieMetadata
                {
                    Runtime = 120
                }
            };
        }

        [Test]
        public void should_match_movie_with_runtime_in_range()
        {
            Subject.Min = 90;
            Subject.Max = 150;

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }

        [Test]
        public void should_not_match_movie_with_runtime_below_min()
        {
            Subject.Min = 150;
            Subject.Max = 180;

            Subject.IsSatisfiedBy(_movie).Should().BeFalse();
        }

        [Test]
        public void should_not_match_movie_with_runtime_above_max()
        {
            Subject.Min = 60;
            Subject.Max = 90;

            Subject.IsSatisfiedBy(_movie).Should().BeFalse();
        }

        [Test]
        public void should_match_negated_movie()
        {
            Subject.Min = 60;
            Subject.Max = 90;
            Subject.Negate = true;

            Subject.IsSatisfiedBy(_movie).Should().BeTrue();
        }
    }
}
