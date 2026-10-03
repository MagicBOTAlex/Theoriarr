using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class ReleaseSearchCacheFixture : TestBase
    {
        private ReleaseSearchCache _subject;

        [SetUp]
        public void Setup()
        {
            _subject = new ReleaseSearchCache();
        }

        [Test]
        public void get_should_return_null_for_unknown_key()
        {
            _subject.Get("missing").Should().BeNull();
        }

        [Test]
        public void store_should_ignore_blank_key()
        {
            _subject.Store(null, new List<DownloadDecision> { Decision("a") });

            _subject.Get(null).Should().BeNull();
        }

        [Test]
        public void store_then_get_should_return_the_decisions()
        {
            _subject.Store("movie:1", new List<DownloadDecision> { Decision("a"), Decision("b") });

            var cached = _subject.Get("movie:1");

            cached.Should().NotBeNull();
            cached.Decisions.Select(GetGuid).Should().BeEquivalentTo(new[] { "a", "b" });
            cached.CachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }

        [Test]
        public void store_should_overwrite_the_previous_result()
        {
            _subject.Store("movie:1", new List<DownloadDecision> { Decision("a") });
            _subject.Store("movie:1", new List<DownloadDecision> { Decision("b") });

            var cached = _subject.Get("movie:1");

            cached.Decisions.Select(GetGuid).Should().BeEquivalentTo(new[] { "b" });
        }

        [Test]
        public void remove_should_drop_the_entry()
        {
            _subject.Store("movie:1", new List<DownloadDecision> { Decision("a") });

            _subject.Remove("movie:1");

            _subject.Get("movie:1").Should().BeNull();
        }

        private static string GetGuid(DownloadDecision decision)
        {
            return (decision.RemoteEpisode?.Release ?? decision.RemoteMovie?.Release)?.Guid;
        }

        private static DownloadDecision Decision(string guid)
        {
            return new DownloadDecision(new RemoteEpisode { Release = new ReleaseInfo { Guid = guid } });
        }
    }
}
