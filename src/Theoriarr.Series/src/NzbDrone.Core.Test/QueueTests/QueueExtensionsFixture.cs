using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using QueueModel = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class QueueExtensionsFixture : CoreTest
    {
        private List<QueueModel> _queue;

        [SetUp]
        public void SetUp()
        {
            _queue = new List<QueueModel>
            {
                new QueueModel { Id = 1, Series = new Series() },
                new QueueModel { Id = 2, Movie = new Movie() },
                new QueueModel { Id = 3 }
            };
        }

        [Test]
        public void series_items_should_exclude_movies()
        {
            _queue.SeriesItems(includeUnknown: true).Select(q => q.Id).Should().BeEquivalentTo(new[] { 1, 3 });
            _queue.SeriesItems(includeUnknown: false).Select(q => q.Id).Should().BeEquivalentTo(new[] { 1 });
        }

        [Test]
        public void movie_items_should_exclude_series()
        {
            _queue.MovieItems(includeUnknown: true).Select(q => q.Id).Should().BeEquivalentTo(new[] { 2, 3 });
            _queue.MovieItems(includeUnknown: false).Select(q => q.Id).Should().BeEquivalentTo(new[] { 2 });
        }
    }
}
