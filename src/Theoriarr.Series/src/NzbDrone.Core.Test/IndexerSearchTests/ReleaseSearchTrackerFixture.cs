using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class ReleaseSearchTrackerFixture : TestBase
    {
        private ReleaseSearchTracker _subject;

        [SetUp]
        public void Setup()
        {
            _subject = new ReleaseSearchTracker();
        }

        [Test]
        public void get_decisions_should_return_empty_for_unknown_search()
        {
            _subject.GetDecisions("missing").Should().BeEmpty();
        }

        [Test]
        public void try_begin_should_only_succeed_once()
        {
            _subject.TryBegin("search-1").Should().BeTrue();
            _subject.TryBegin("search-1").Should().BeFalse();
            _subject.TryBegin("search-2").Should().BeTrue();
        }

        [Test]
        public void recorded_indexers_should_accumulate_decisions()
        {
            _subject.TryBegin("search-1");

            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a"), Decision("b") });
            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("c") });

            _subject.GetDecisions("search-1").Should().HaveCount(3);
        }

        [Test]
        public void recorded_decisions_should_be_deduped_by_guid()
        {
            _subject.TryBegin("search-1");

            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a") });
            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a"), Decision("b") });

            _subject.GetDecisions("search-1").Should().HaveCount(2);
        }

        [Test]
        public void complete_should_replace_accumulated_decisions()
        {
            _subject.TryBegin("search-1");

            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a") });
            _subject.Complete("search-1", new List<DownloadDecision> { Decision("z") });

            var decisions = _subject.GetDecisions("search-1");

            decisions.Should().HaveCount(1);
            decisions[0].RemoteEpisode.Release.Guid.Should().Be("z");
        }

        [Test]
        public void complete_should_keep_accumulated_decisions_when_none_supplied()
        {
            _subject.TryBegin("search-1");

            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a") });
            _subject.Complete("search-1", null);

            _subject.GetDecisions("search-1").Should().HaveCount(1);
        }

        [Test]
        public void cancel_should_drop_the_search_and_its_token()
        {
            _subject.TryBegin("search-1");
            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a") });

            var token = _subject.GetToken("search-1");

            token.IsCancellationRequested.Should().BeFalse();

            _subject.Cancel("search-1");

            token.IsCancellationRequested.Should().BeTrue();
            _subject.GetDecisions("search-1").Should().BeEmpty();
        }

        [Test]
        public void records_should_be_scoped_to_their_search()
        {
            _subject.TryBegin("search-1");
            _subject.TryBegin("search-2");

            _subject.RecordIndexer("search-1", new List<DownloadDecision> { Decision("a") });

            _subject.GetDecisions("search-2").Should().BeEmpty();
        }

        private static DownloadDecision Decision(string guid)
        {
            return new DownloadDecision(new RemoteEpisode { Release = new ReleaseInfo { Guid = guid } });
        }
    }
}
