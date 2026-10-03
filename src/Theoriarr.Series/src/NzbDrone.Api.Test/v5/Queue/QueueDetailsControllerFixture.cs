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

namespace NzbDrone.Api.Test.v5.Queue
{
    [TestFixture]
    public class QueueDetailsControllerFixture : TestBase<QueueDetailsController>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPendingQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        private static NzbDrone.Core.Queue.Queue SeriesItem(int episodeId)
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 1,
                Series = new Series { Id = 10 },
                Episodes = new List<Episode> { new Episode { Id = episodeId } },
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

        private void GivenQueue(params NzbDrone.Core.Queue.Queue[] items)
        {
            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(items.ToList());
        }

        [Test]
        public void get_queue_should_not_throw_for_movie_items_when_filtering_by_episode_ids()
        {
            // The shared queue contains movie items whose Episodes is null; the episodeIds
            // filter must skip them instead of dereferencing the null collection.
            GivenQueue(SeriesItem(1), MovieItem());

            var result = Subject.GetQueue(null, new List<int> { 1 }, null);

            result.Value.Should().HaveCount(1);
            result.Value.Single().Id.Should().Be(1);
        }

        [Test]
        public void get_queue_should_match_series_items_by_episode_ids()
        {
            GivenQueue(SeriesItem(1), MovieItem());

            var result = Subject.GetQueue(null, new List<int> { 1 }, new[] { QueueSubresource.Episodes });

            result.Value.Should().HaveCount(1);
            result.Value.Single().EpisodeIds.Should().Contain(1);
        }

        [Test]
        public void get_queue_should_not_throw_for_movie_items_without_subresources()
        {
            GivenQueue(MovieItem());

            var result = Subject.GetQueue(null, new List<int>(), null);

            result.Value.Should().HaveCount(1);
        }

        [Test]
        public void get_queue_should_map_movie_id_for_movie_items()
        {
            // The SPA's Library matches movie downloads by MovieId, which must survive the
            // series-shaped V5 queue resource used for the merged queue.
            GivenQueue(MovieItem());

            var result = Subject.GetQueue(null, new List<int>(), null);

            result.Value.Single().MovieId.Should().Be(20);
        }
    }
}
