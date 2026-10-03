using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class MovieRepositoryFixture : DbTest<MovieRepository, Movie>
    {
        [Test]
        public void all_should_not_throw_when_quality_profile_is_missing()
        {
            Mocker.GetMock<IQualityProfileRepository>().Setup(x => x.All()).Returns(new List<QualityProfile>());
            Mocker.GetMock<IAlternativeTitleRepository>().Setup(x => x.All()).Returns(new List<AlternativeTitle>());

            var metadata = new MovieMetadata
            {
                Title = "Missing Profile",
                SortTitle = "missing profile",
                TmdbId = 1,
                Year = 2000
            };
            metadata.Id = Db.Insert(metadata).Id;

            var movie = new Movie
            {
                MovieMetadataId = metadata.Id,
                QualityProfileId = 999,
                Path = "/movies/missing-profile",
                Monitored = true
            };
            movie.Id = Db.Insert(movie).Id;

            var result = Subject.All().ToList();

            result.Should().ContainSingle();
            result[0].QualityProfile.Should().BeNull();
        }
    }
}
