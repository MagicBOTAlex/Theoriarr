using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Profiles.Quality;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Profiles.Quality
{
    [TestFixture]
    public class QualityProfileControllerFixture : TestBase<QualityProfileController>
    {
        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private void GivenUsage(List<Series> series, List<Movie> movies)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(series);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetAllMovies())
                  .Returns(movies);
        }

        private static QualityProfile Profile(int id, string name, MediaType mediaType)
        {
            return new QualityProfile
            {
                Id = id,
                Name = name,
                MediaType = mediaType,
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem>()
            };
        }

        [Test]
        public void get_all_should_fall_back_to_unfiltered_for_movie_key_ac2()
        {
            // AC2: the only seeded profile is shared and used by a series; the movie key
            // must still receive it.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new List<Series> { new Series { Id = 1, QualityProfileId = 1 } },
                new List<Movie>());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { Profile(1, "Shared", MediaType.Series) });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
        }

        [Test]
        public void get_all_should_omit_shared_series_only_profile_when_movie_profile_exists_ac5()
        {
            // AC5: a movie-visible alternative keeps the narrowing.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new List<Series> { new Series { Id = 1, QualityProfileId = 1 } },
                new List<Movie>());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile>
                  {
                      Profile(1, "Shared", MediaType.Series),
                      Profile(2, "Movie", MediaType.Movie)
                  });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(2);
        }

        [Test]
        public void update_should_return_accepted_with_written_body_for_hidden_row()
        {
            // R4: a PUT targeting a domain-hidden row commits the change. The response must
            // be a 202 carrying the written resource, not a 404 from a domain-scoped re-read.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new List<Series>(), new List<Movie>());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.Get(9))
                  .Returns(Profile(9, "Movie", MediaType.Movie));

            var result = Subject.Update(new QualityProfileResource
            {
                Id = 9,
                Name = "Movie",
                Items = new List<QualityProfileQualityItemResource>(),
                FormatItems = new List<ProfileFormatItemResource>()
            });

            var accepted = result.Result.Should().BeOfType<AcceptedAtActionResult>().Subject;
            var resource = accepted.Value.Should().BeOfType<QualityProfileResource>().Subject;

            resource.Id.Should().Be(9);
            resource.Name.Should().Be("Movie");
        }

        [Test]
        public void get_by_id_should_return_shared_movie_only_profile_to_series_ac1()
        {
            // AC1: by-id read of a shared profile referenced only by movies stays available
            // to the series key.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(
                new List<Series>(),
                new List<Movie> { new Movie { Id = 1, QualityProfileId = 3 } });

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.Get(3))
                  .Returns(Profile(3, "Shared", MediaType.Series));

            var result = Subject.GetResourceByIdWithErrorHandler(3);

            var ok = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<QualityProfileResource>>().Subject;
            ok.Value.Should().NotBeNull();
            ok.Value.Id.Should().Be(3);
        }

        [Test]
        public void get_by_id_should_return_404_for_movie_only_profile_with_series_key()
        {
            // B3: hidden by-id read returns 404, never 200 with an empty body.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new List<Series>(), new List<Movie>());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.Get(9))
                  .Returns(Profile(9, "Movie", MediaType.Movie));

            Subject.Invoking(s => s.GetResourceByIdWithErrorHandler(9))
                   .Should().Throw<Sonarr.Http.REST.NotFoundException>()
                   .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
