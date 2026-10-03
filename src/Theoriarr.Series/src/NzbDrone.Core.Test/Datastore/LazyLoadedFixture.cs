using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class LazyLoadedFixture
    {
        [Test]
        public void should_only_load_once_when_accessed_concurrently()
        {
            var loadCount = 0;

            var lazy = new LazyLoaded<object, object>(
                (database, parent) =>
                {
                    Interlocked.Increment(ref loadCount);
                    Thread.Sleep(50);
                    return new object();
                },
                parent => true);

            lazy.Prepare(null, new object());

            var results = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => lazy.Value))
                .ToArray();

            Task.WaitAll(results);

            loadCount.Should().Be(1);
            results.Select(r => r.Result).Distinct().Should().HaveCount(1);
        }
    }
}
