using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    public class NewznabSettingFixture : CoreTest
    {
        [TestCase("http://nzbs.org")]
        [TestCase("http:///www.nzbplanet.net")]
        public void requires_apikey(string url)
        {
            var setting = new NewznabSettings()
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeFalse();
            setting.Validate().Errors.Should().Contain(c => c.PropertyName == nameof(NewznabSettings.ApiKey));
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase(null)]
        public void invalid_url_should_not_apikey(string url)
        {
            var setting = new NewznabSettings
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeFalse();
            setting.Validate().Errors.Should().NotContain(c => c.PropertyName == nameof(NewznabSettings.ApiKey));
            setting.Validate().Errors.Should().Contain(c => c.PropertyName == nameof(NewznabSettings.BaseUrl));
        }

        [TestCase("http://nzbs2.org")]
        public void doesnt_requires_apikey(string url)
        {
            var setting = new NewznabSettings()
            {
                ApiKey = "",
                BaseUrl = url
            };

            setting.Validate().IsValid.Should().BeTrue();
        }

        [Test]
        public void should_leave_categories_untouched_when_splitting_at_search_time()
        {
            var setting = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040, 2000, 2010 },
                MovieCategories = new[] { 2000 }
            };

            setting.GetTvCategories().Should().BeEquivalentTo(new[] { 5030, 5040 });
            setting.GetMovieCategories().Should().BeEquivalentTo(new[] { 2000, 2010 });

            // The stored value must round-trip byte-identical for Prowlarr's equality check.
            setting.Categories.Should().BeEquivalentTo(new[] { 5030, 5040, 2000, 2010 });
            setting.MovieCategories.Should().BeEquivalentTo(new[] { 2000 });
        }

        [Test]
        public void should_fall_back_to_standard_movie_categories_from_a_tv_only_category_list()
        {
            var setting = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 5030, 5040 },
                MovieCategories = System.Array.Empty<int>()
            };

            setting.GetTvCategories().Should().BeEquivalentTo(new[] { 5030, 5040 });

            // A TV-only indexer (Prowlarr's Sonarr sync) must still be searched for movies; the
            // fallback is search-time only and does not change the stored value.
            setting.GetMovieCategories().Should().BeEquivalentTo(NewznabSettings.DefaultMovieCategories);
            setting.MovieCategories.Should().BeEmpty();
        }

        [Test]
        public void should_not_persist_the_movie_category_fallback()
        {
            var setting = new NewznabSettings();

            setting.MovieCategories.Should().BeEmpty();
            setting.GetMovieCategories().Should().BeEquivalentTo(NewznabSettings.DefaultMovieCategories);
        }

        [Test]
        public void should_derive_movie_categories_from_categories_at_use_time()
        {
            var setting = new NewznabSettings
            {
                BaseUrl = "http://indexer.local",
                Categories = new[] { 2040 }
            };

            setting.MovieCategories.Should().BeEmpty();
            setting.GetMovieCategories().Should().BeEquivalentTo(new[] { 2040 });
        }
    }
}
