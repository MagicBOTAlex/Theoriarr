using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Tags;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Tags;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Tags
{
    [TestFixture]
    public class TagDetailsControllerFixture : TestBase<TagDetailsController>
    {
        private TagDetails GivenDetails(int id, IEnumerable<int> seriesIds = null, IEnumerable<int> movieIds = null)
        {
            return new TagDetails
            {
                Id = id,
                Label = $"tag-{id}",
                SeriesIds = seriesIds == null ? new List<int>() : new List<int>(seriesIds),
                MovieIds = movieIds == null ? new List<int>() : new List<int>(movieIds),
                DelayProfileIds = new List<int>(),
                ImportListIds = new List<int>(),
                NotificationIds = new List<int>(),
                RestrictionIds = new List<int>(),
                ExcludedReleaseProfileIds = new List<int>(),
                IndexerIds = new List<int>(),
                DownloadClientIds = new List<int>(),
                AutoTagIds = new List<int>()
            };
        }

        private void GivenSubsystem(AppSubsystem subsystem)
        {
            Mocker.GetMock<ISubsystemAccessor>()
                  .SetupGet(a => a.Subsystem)
                  .Returns(subsystem);
        }

        private void GivenTagMediaType(int id, MediaType mediaType)
        {
            Mocker.GetMock<ITagService>()
                  .Setup(s => s.GetTag(id))
                  .Returns(new Tag { Id = id, Label = $"tag-{id}", MediaType = mediaType });
        }

        [Test]
        public void get_by_id_should_return_404_for_series_only_details_with_movie_key()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenTagMediaType(5, MediaType.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Details(5))
                  .Returns(GivenDetails(5, new[] { 1 }));

            Subject.Invoking(s => s.GetResourceByIdWithErrorHandler(5))
                   .Should().Throw<Sonarr.Http.REST.NotFoundException>()
                   .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public void get_by_id_should_return_details_for_series_key()
        {
            GivenSubsystem(AppSubsystem.Series);
            GivenTagMediaType(5, MediaType.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Details(5))
                  .Returns(GivenDetails(5, new[] { 1 }));

            var result = Subject.GetResourceByIdWithErrorHandler(5);

            result.Result.Should().BeOfType<Ok<TagDetailsResource>>()
                  .Subject.Value.Id.Should().Be(5);
        }

        [Test]
        public void get_by_id_should_return_shared_tag_used_by_series_and_movie_to_movie_key()
        {
            // R3: a shared Series(0) tag referenced by a movie (in addition to a series)
            // must stay visible to the movie key instead of being treated as series-only.
            GivenSubsystem(AppSubsystem.Movies);
            GivenTagMediaType(5, MediaType.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Details(5))
                  .Returns(GivenDetails(5, new[] { 1 }, new[] { 99 }));

            var result = Subject.GetResourceByIdWithErrorHandler(5);

            var resource = result.Result.Should().BeOfType<Ok<TagDetailsResource>>().Subject.Value;
            resource.Id.Should().Be(5);
            resource.MovieIds.Should().Equal(99);
        }

        [Test]
        public void get_by_id_should_return_movie_only_tag_to_movie_key()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenTagMediaType(5, MediaType.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Details(5))
                  .Returns(GivenDetails(5, movieIds: new[] { 99 }));

            var result = Subject.GetResourceByIdWithErrorHandler(5);

            result.Result.Should().BeOfType<Ok<TagDetailsResource>>()
                  .Subject.Value.Id.Should().Be(5);
        }

        [Test]
        public void get_all_should_fall_back_for_movie_key_when_nothing_visible()
        {
            GivenSubsystem(AppSubsystem.Movies);
            GivenTagMediaType(5, MediaType.Series);

            Mocker.GetMock<ITagService>()
                  .Setup(s => s.Details())
                  .Returns(new List<TagDetails> { GivenDetails(5, new[] { 1 }) });

            var result = Subject.GetAll();

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(5);
        }
    }
}
