using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Clients;

namespace NzbDrone.Core.Test.Download.DownloadClientTests
{
    [TestFixture]
    public class DownloadClientCategoryHelperFixture
    {
        [TestCase("theoriarr", "theoriarr", false)]
        [TestCase("sonarr", "radarr", true)]
        [TestCase("radarr", "sonarr", true)]
        [TestCase(null, "radarr", true)]
        [TestCase("radarr", null, false)]
        [TestCase("radarr", "", false)]
        public void CategoriesAreDistinct_should_only_be_true_for_two_configured_different_categories(string seriesCategory, string movieCategory, bool expected)
        {
            var settings = new TestCategorySettings(seriesCategory, movieCategory);

            DownloadClientCategoryHelper.CategoriesAreDistinct(settings).Should().Be(expected);
        }

        [Test]
        public void CategoriesAreDistinct_should_be_false_for_null_settings()
        {
            DownloadClientCategoryHelper.CategoriesAreDistinct(null).Should().BeFalse();
        }

        [Test]
        public void IsSeriesCategory_should_match_a_distinct_series_category()
        {
            var settings = new TestCategorySettings("sonarr", "radarr");

            DownloadClientCategoryHelper.IsSeriesCategory("sonarr", settings).Should().BeTrue();
        }

        [Test]
        public void IsSeriesCategory_should_not_match_the_shared_category()
        {
            var settings = new TestCategorySettings("theoriarr", "theoriarr");

            DownloadClientCategoryHelper.IsSeriesCategory("theoriarr", settings).Should().BeFalse();
        }

        [Test]
        public void IsSeriesCategory_should_be_false_for_null_settings()
        {
            DownloadClientCategoryHelper.IsSeriesCategory("sonarr", null).Should().BeFalse();
        }

        [Test]
        public void EffectiveMovieCategory_should_use_the_movie_category_when_configured()
        {
            var settings = new TestCategorySettings("theoriarr", "movies");

            DownloadClientCategoryHelper.EffectiveMovieCategory(settings).Should().Be("movies");
        }

        [Test]
        public void EffectiveMovieCategory_should_fall_back_to_the_series_category()
        {
            var settings = new TestCategorySettings("theoriarr", null);

            DownloadClientCategoryHelper.EffectiveMovieCategory(settings).Should().Be("theoriarr");
        }

        [Test]
        public void EffectiveMovieCategory_should_fall_back_to_the_series_category_when_the_movie_category_is_blank()
        {
            var settings = new TestCategorySettings("theoriarr", " ");

            DownloadClientCategoryHelper.EffectiveMovieCategory(settings).Should().Be("theoriarr");
        }

        [Test]
        public void MatchesCategory_should_match_the_series_category()
        {
            DownloadClientCategoryHelper.MatchesCategory("theoriarr", "theoriarr", null).Should().BeTrue();
        }

        [Test]
        public void MatchesCategory_should_match_the_movie_category()
        {
            DownloadClientCategoryHelper.MatchesCategory("movies", "theoriarr", "movies").Should().BeTrue();
        }

        [Test]
        public void MatchesCategory_should_not_match_an_unrelated_category()
        {
            DownloadClientCategoryHelper.MatchesCategory("other", "theoriarr", "movies").Should().BeFalse();
        }

        [Test]
        public void MatchesCategory_should_match_anything_when_the_series_category_is_blank()
        {
            DownloadClientCategoryHelper.MatchesCategory("other", null, null).Should().BeTrue();
        }

        [Test]
        public void MatchesCategory_should_not_match_blank_when_a_series_category_is_configured()
        {
            DownloadClientCategoryHelper.MatchesCategory(null, "theoriarr", null).Should().BeFalse();
        }

        private class TestCategorySettings : IDownloadClientCategorySettings
        {
            public TestCategorySettings(string seriesCategory, string movieCategory)
            {
                SeriesCategory = seriesCategory;
                MovieCategory = movieCategory;
            }

            public string SeriesCategory { get; }

            public string MovieCategory { get; }
        }
    }
}
