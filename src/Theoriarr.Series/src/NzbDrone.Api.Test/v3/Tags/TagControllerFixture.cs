using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Tags;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Tags
{
    [TestFixture]
    public class TagControllerFixture : TestBase<TagController>
    {
        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private void GivenUsage(Dictionary<int, List<int>> seriesTags, Dictionary<int, List<int>> movieTags)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesTags())
                  .Returns(seriesTags);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMovieTags())
                  .Returns(movieTags);
        }

        [Test]
        public void create_should_return_reused_movie_tag_body_for_series_key()
        {
            // B1: the label already exists as an explicit Movie row; TagService.Add recycles
            // it. The series key must still receive a TagResource (never a null body).
            GivenSubsystem(AppSubsystem.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Add(It.IsAny<Tag>()))
                  .Returns(new Tag { Id = 7, Label = "foo", MediaType = MediaType.Movie });

            var result = Subject.Create(new TagResource { Label = "foo" });

            var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var resource = created.Value.Should().BeOfType<TagResource>().Subject;

            resource.Id.Should().Be(7);
            resource.Label.Should().Be("foo");
        }

        [Test]
        public void get_by_id_should_return_404_for_movie_only_tag_with_series_key()
        {
            // B3: a hidden by-id read must be a 404, not a 200 with an empty body.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, List<int>>(), new Dictionary<int, List<int>>());

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.GetTag(7))
                  .Returns(new Tag { Id = 7, Label = "foo", MediaType = MediaType.Movie });

            Subject.Invoking(s => s.GetResourceByIdWithErrorHandler(7))
                   .Should().Throw<Sonarr.Http.REST.NotFoundException>()
                   .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public void update_should_return_accepted_with_written_body_for_hidden_row()
        {
            // R4: a PUT targeting a domain-hidden row commits the change. The response must
            // be a 202 carrying the written resource, not a 404 from a domain-scoped re-read.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(new Dictionary<int, List<int>>(), new Dictionary<int, List<int>>());

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.GetTag(7))
                  .Returns(new Tag { Id = 7, Label = "movie-only", MediaType = MediaType.Movie });

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Update(It.IsAny<Tag>()))
                  .Returns((Tag t) => t);

            var result = Subject.Update(new TagResource { Id = 7, Label = "movie-only" });

            var accepted = result.Result.Should().BeOfType<AcceptedAtActionResult>().Subject;
            var resource = accepted.Value.Should().BeOfType<TagResource>().Subject;

            resource.Id.Should().Be(7);
            resource.Label.Should().Be("movie-only");
        }

        [Test]
        public void get_all_should_fall_back_to_unfiltered_for_movie_key_ac2()
        {
            // AC2: the only tag is shared and used by a series; the movie list must not be
            // empty or Jellyseerr cannot configure the connection.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, List<int>> { { 1, new List<int> { 1 } } },
                new Dictionary<int, List<int>>());

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.All())
                  .Returns(new List<Tag>
                  {
                      new Tag { Id = 1, Label = "shared-series-only", MediaType = MediaType.Series }
                  });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(1);
        }

        [Test]
        public void get_all_should_omit_shared_series_only_tag_when_movie_tag_exists_ac5()
        {
            // AC5: a movie-visible alternative keeps the narrowing.
            GivenSubsystem(AppSubsystem.Movies);
            GivenUsage(
                new Dictionary<int, List<int>> { { 1, new List<int> { 1 } } },
                new Dictionary<int, List<int>>());

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.All())
                  .Returns(new List<Tag>
                  {
                      new Tag { Id = 1, Label = "shared-series-only", MediaType = MediaType.Series },
                      new Tag { Id = 2, Label = "movie-only", MediaType = MediaType.Movie }
                  });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(2);
        }

        [Test]
        public void get_all_should_show_shared_tags_to_series_ac1()
        {
            // AC1: a shared Series(0) tag referenced only by movies is still visible to the
            // series key.
            GivenSubsystem(AppSubsystem.Series);
            GivenUsage(
                new Dictionary<int, List<int>>(),
                new Dictionary<int, List<int>> { { 1, new List<int> { 3 } } });

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.All())
                  .Returns(new List<Tag>
                  {
                      new Tag { Id = 3, Label = "shared-movie-only", MediaType = MediaType.Series }
                  });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(3);
        }
    }
}
