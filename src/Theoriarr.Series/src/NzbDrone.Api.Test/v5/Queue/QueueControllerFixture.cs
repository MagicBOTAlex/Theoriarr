using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Queue;
using Sonarr.Http;

namespace NzbDrone.Api.Test.v5.Queue
{
    [TestFixture]
    public class QueueControllerFixture : TestBase<QueueController>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        private static NzbDrone.Core.Queue.Queue SeriesItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Series = new Series { Id = 10 },
                Episodes = new List<Episode> { new Episode { Id = 100 } },
                Languages = new List<Language>()
            };
        }

        private static NzbDrone.Core.Queue.Queue MovieItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 2,
                Movie = new Movie { Id = 20 },
                RemoteMovie = new RemoteMovie(),
                Languages = new List<Language>()
            };
        }

        private static NzbDrone.Core.Queue.Queue UnknownItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 3,
                Languages = new List<Language>()
            };
        }

        private void GivenQueue(params NzbDrone.Core.Queue.Queue[] items)
        {
            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(items.ToList());
        }

        private PagingResource<QueueResource> GetQueue(bool includeUnknownSeriesItems)
        {
            return Subject.GetQueue(
                new PagingRequestResource(),
                includeUnknownSeriesItems,
                null,
                null,
                null,
                null,
                null,
                new[] { QueueSubresource.Series }).Value;
        }

        [Test]
        public void get_queue_should_exclude_movie_items()
        {
            GivenQueue(SeriesItem(), MovieItem());

            var result = GetQueue(true);

            result.Records.Should().HaveCount(1);
            result.Records.Single().Id.Should().Be(1);
            result.Records.Single().MovieId.Should().BeNull();
        }

        [Test]
        public void get_queue_should_include_unknown_items_when_requested()
        {
            GivenQueue(SeriesItem(), MovieItem(), UnknownItem());

            var result = GetQueue(true);

            result.Records.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 3 });
        }

        [Test]
        public void get_queue_should_exclude_unknown_items_when_not_requested()
        {
            GivenQueue(SeriesItem(), MovieItem(), UnknownItem());

            var result = GetQueue(false);

            result.Records.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1 });
        }
    }
}
