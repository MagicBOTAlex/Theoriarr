using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class SeriesTitleMatcherFixture : CoreTest<SeriesTitleMatcher>
    {
        private const string SeriesTitle = "Sample Show";
        private const string SceneAlias = "Sample Alias";
        private const int TvdbId = 123456;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series>());

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>());
        }

        private static Series BuildSeries(int id, string title, int tvdbId)
        {
            return new Series { Id = id, Title = title, TvdbId = tvdbId };
        }

        private void GivenSeries(Series series, params string[] sceneNames)
        {
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { series });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(series.Id))
                .Returns(series);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(series.TvdbId))
                .Returns(sceneNames.Select(n => new SceneMapping { TvdbId = series.TvdbId, Title = n }).ToList());
        }

        [Test]
        public void should_match_series_by_scene_alias_with_different_separators()
        {
            if (!ExternalTestFixtures.IsAvailable)
            {
                Assert.Pass("External copyrighted test fixtures not provided.");
            }

            var data = ExternalTestFixtures.Data;
            var series = BuildSeries(1, data.SeriesTitle, data.TvdbId);
            GivenSeries(series, data.SceneAlias);

            Subject.Match(data.JoinedParsedTitle).Should().Be(series);
        }

        [Test]
        public void should_match_the_series_title_exactly()
        {
            var series = BuildSeries(1, SeriesTitle, TvdbId);
            GivenSeries(series, SceneAlias);

            Subject.Match(SeriesTitle).Should().Be(series);
        }

        [Test]
        public void should_match_a_year_suffixed_series_title_ignoring_the_year()
        {
            var series = BuildSeries(1, "Sample Show (2022)", TvdbId);
            GivenSeries(series, SceneAlias);

            Subject.Match(SeriesTitle).Should().Be(series);
        }

        [Test]
        public void should_not_attach_a_longer_parsed_title_to_a_shorter_series_title()
        {
            var series = BuildSeries(1, SeriesTitle, TvdbId);
            GivenSeries(series, SceneAlias);

            Subject.Match("Sample Show Two").Should().BeNull();
        }

        [Test]
        public void should_prefer_the_spin_off_title_over_the_parent_series()
        {
            var parent = BuildSeries(1, SeriesTitle, 111);
            var spinOff = BuildSeries(2, "Sample Show Two", 222);

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { parent, spinOff });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(It.IsAny<int>()))
                .Returns<int>(id => id == parent.Id ? parent : spinOff);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>());

            Subject.Match("Sample Show Two").Should().Be(spinOff);
        }

        [Test]
        public void should_return_null_when_nothing_matches()
        {
            var series = BuildSeries(1, SeriesTitle, TvdbId);
            GivenSeries(series, SceneAlias);

            Subject.Match("Some Completely Different Show").Should().BeNull();
        }

        [Test]
        public void should_return_null_when_the_match_is_ambiguous()
        {
            var first = BuildSeries(1, "First Sample Show", 111);
            var second = BuildSeries(2, "Second Sample Show", 222);

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { first, second });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(It.IsAny<int>()))
                .Returns<int>(id => id == first.Id ? first : second);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(first.TvdbId))
                .Returns(new List<SceneMapping> { new SceneMapping { TvdbId = first.TvdbId, Title = SceneAlias } });

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(second.TvdbId))
                .Returns(new List<SceneMapping> { new SceneMapping { TvdbId = second.TvdbId, Title = SceneAlias } });

            Subject.Match(SceneAlias).Should().BeNull();
        }

        [Test]
        public void should_restrict_the_match_to_the_given_series()
        {
            var searched = BuildSeries(1, SeriesTitle, TvdbId);
            var other = BuildSeries(2, "Other Sample Show", 999);

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetAllSeries())
                .Returns(new List<Series> { searched, other });

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(It.IsAny<int>()))
                .Returns<int>(id => id == searched.Id ? searched : other);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(searched.TvdbId))
                .Returns(new List<SceneMapping> { new SceneMapping { TvdbId = searched.TvdbId, Title = SceneAlias } });

            Subject.Match(SceneAlias, searched).Should().Be(searched);
            Subject.Match(SceneAlias, other).Should().BeNull();
        }
    }
}
