using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.History;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Movies.Indexers;

namespace NzbDrone.Api.Test.v3.Movies
{
    [TestFixture]
    public class MovieReleaseControllerFixture : TestBase<ReleaseController>
    {
        private RemoteMovie _remoteMovie;

        [SetUp]
        public void Setup()
        {
            var movie = Builder<Movie>.CreateNew()
                .With(m => m.Id = 1)
                .With(m => m.Title = "A Movie")
                .Build();

            _remoteMovie = new RemoteMovie
            {
                Movie = movie,
                ParsedMovieInfo = new ParsedMovieInfo
                {
                    MovieTitles = new List<string> { "A Movie" },
                    Year = 1998,
                    Quality = new QualityModel(Quality.Bluray1080p)
                },
                Release = new ReleaseInfo { Guid = "abc", Title = "A.Movie.1998.1080p.BluRay.x264-GROUP" },
                Languages = new List<Language> { Language.English },
                DownloadAllowed = true
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.MovieSearch(1, true, true))
                  .ReturnsAsync(new List<DownloadDecision> { new DownloadDecision(_remoteMovie) });

            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(s => s.PrioritizeDecisionsForMovies(It.IsAny<List<DownloadDecision>>()))
                  .Returns<List<DownloadDecision>>(d => d);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByMovieId(1))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.GetDefaultProfile(It.IsAny<string>()))
                  .Returns(new QualityProfile
                  {
                      Items = new List<QualityProfileQualityItem>
                      {
                          new QualityProfileQualityItem { Quality = Quality.Bluray1080p, Allowed = true }
                      }
                  });
        }

        [Test]
        public async Task should_return_movie_release_with_non_null_remote_movie()
        {
            var result = await Subject.GetReleases(1);

            result.Should().HaveCount(1);
            result.Single().MappedMovieId.Should().Be(1);
            result.Single().Title.Should().Be("A.Movie.1998.1080p.BluRay.x264-GROUP");
            result.Single().Approved.Should().BeTrue();
        }
    }
}
