using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    public class NewznabRequestGeneratorFixture : CoreTest<NewznabRequestGenerator>
    {
        private MovieSearchCriteria _movieSearchCriteria;
        private SingleEpisodeSearchCriteria _singleEpisodeSearchCriteria;
        private SeasonSearchCriteria _seasonSearchCriteria;
        private AnimeEpisodeSearchCriteria _animeSearchCriteria;
        private AnimeSeasonSearchCriteria _animeSeasonSearchCriteria;
        private NewznabCapabilities _capabilities;

        [SetUp]
        public void SetUp()
        {
            Subject.Definition = new IndexerDefinition
            {
                Name = "Newznab"
            };

            Subject.Settings = new NewznabSettings()
            {
                BaseUrl = "http://127.0.0.1:1234/",
                Categories = new[] { 1, 2 },
                AnimeCategories = new[] { 3, 4 },
                MovieCategories = new[] { 5, 6 },
                ApiKey = "abcd",
            };

            _movieSearchCriteria = new MovieSearchCriteria
            {
                Movie = new Movies.Movie { ImdbId = "tt0076759", Title = "Star Wars", Year = 1977, TmdbId = 11 },
                SceneTitles = new List<string> { "Star Wars" }
            };

            _singleEpisodeSearchCriteria = new SingleEpisodeSearchCriteria
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 1,
                EpisodeNumber = 2
            };

            _seasonSearchCriteria = new SeasonSearchCriteria
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 1,
            };

            _animeSearchCriteria = new AnimeEpisodeSearchCriteria()
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string>() { "Monkey+Island" },
                AbsoluteEpisodeNumber = 100,
                SeasonNumber = 5,
                EpisodeNumber = 4
            };

            _animeSeasonSearchCriteria = new AnimeSeasonSearchCriteria()
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 3,
            };

            _capabilities = new NewznabCapabilities();

            Mocker.GetMock<INewznabCapabilitiesProvider>()
                .Setup(v => v.GetCapabilities(It.IsAny<NewznabSettings>()))
                .Returns(_capabilities);
        }

        [Test]
        public void should_use_tv_categories_for_tv_feed_and_movie_categories_for_movie_feed()
        {
            var results = Subject.GetRecentRequests();

            results.GetAllTiers().Should().HaveCount(2);

            var tvRequest = results.GetTier(0).First().First();
            tvRequest.Url.FullUri.Should().Contain("?t=tvsearch&");
            tvRequest.Url.Query.Should().Contain("&cat=1,2,3,4&");

            var movieRequest = results.GetTier(1).First().First();
            movieRequest.Url.FullUri.Should().Contain("?t=movie&");
            movieRequest.Url.Query.Should().Contain("&cat=5,6&");
        }

        [Test]
        public void should_not_have_duplicate_categories()
        {
            Subject.Settings.Categories = new[] { 1, 2, 2, 3 };
            Subject.Settings.AnimeCategories = new int[0];

            var results = Subject.GetRecentRequests();

            var tvRequest = results.GetTier(0).First().First();

            tvRequest.Url.Query.Should().Contain("&cat=1,2,3&");
        }

        [Test]
        public void should_use_movie_categories_for_movie_search()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "imdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("&cat=5,6&");
            page.Url.Query.Should().NotContain("&cat=1,2");
        }

        [Test]
        public void should_return_subsequent_movie_pages()
        {
            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetAllTiers().First().Take(3).ToList();

            pages[0].Url.FullUri.Should().Contain("&offset=0&");
            pages[1].Url.FullUri.Should().Contain("&offset=100&");
            pages[2].Url.FullUri.Should().Contain("&offset=200&");
        }

        [Test]
        public void should_not_get_unlimited_movie_pages()
        {
            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetAllTiers().First().Take(500).ToList();

            pages.Count.Should().BeLessThan(500);
        }

        [Test]
        public void should_not_search_by_imdbid_if_not_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().NotContain("imdbid=0076759");
            page.Url.Query.Should().Contain("q=Star");
        }

        [Test]
        public void should_search_movie_by_imdbid_if_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "imdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("imdbid=0076759");
        }

        [Test]
        public void should_search_by_tmdbid_if_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "tmdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tmdbid=11");
        }

        [Test]
        public void should_prefer_search_by_tmdbid_if_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "tmdbid", "imdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tmdbid=11");
            page.Url.Query.Should().NotContain("imdbid=0076759");
        }

        [Test]
        public void should_use_aggregrated_id_search_for_movie_if_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "tmdbid", "imdbid" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("tmdbid=11");
            page.Url.Query.Should().Contain("imdbid=0076759");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_for_movie_if_no_ids_supported()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.Tiers.Should().Be(1);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_for_movie_if_no_ids_are_known()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "imdbid" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            _movieSearchCriteria.Movie.ImdbId = null;

            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_fallback_to_q_for_movie()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q", "tmdbid", "imdbid" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tmdbid=11");
            pageTier2.Url.Query.Should().NotContain("imdbid=0076759");
            pageTier2.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_encode_raw_movie_title()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q" };
            _capabilities.MovieTextSearchEngine = "raw";

            var movieRawSearchCriteria = new MovieSearchCriteria
            {
                Movie = new Movies.Movie { Title = "Some Movie & Title: Words", Year = 2021, TmdbId = 123 },
                SceneTitles = new List<string> { "Some Movie & Title: Words" }
            };

            var results = Subject.GetSearchRequests(movieRawSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=Some%20Movie%20%26%20Title%3A%20Words");
            page.Url.Query.Should().NotContain(" & ");
            page.Url.Query.Should().Contain("%26");
        }

        [Test]
        public void should_use_clean_movie_title_and_encode()
        {
            _capabilities.SupportedMovieSearchParameters = new[] { "q" };
            _capabilities.MovieTextSearchEngine = "sphinx";

            var movieRawSearchCriteria = new MovieSearchCriteria
            {
                Movie = new Movies.Movie { Title = "Some Movie & Title: Words", Year = 2021, TmdbId = 123 },
                SceneTitles = new List<string> { "Some Movie & Title: Words" }
            };

            var results = Subject.GetSearchRequests(movieRawSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=Some%20Movie%20and%20Title%20Words%202021");
            page.Url.Query.Should().Contain("and");
            page.Url.Query.Should().NotContain(" & ");
            page.Url.Query.Should().NotContain("%26");
        }

        [Test]
        public void should_use_only_anime_categories_for_anime_search()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("&cat=3,4&");
            pages[1].Url.FullUri.Should().Contain("&cat=3,4&");
        }

        [Test]
        public void should_use_mode_search_for_anime()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            results.GetAllTiers().First().First().Url.FullUri.Should().Contain("?t=tvsearch&");
            results.GetAllTiers().Last().First().Url.FullUri.Should().Contain("?t=search&");
        }

        [Test]
        public void should_return_subsequent_pages()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetAllTiers().First().Take(3).ToList();

            pages[0].Url.FullUri.Should().Contain("&offset=0&");
            pages[1].Url.FullUri.Should().Contain("&offset=100&");
            pages[2].Url.FullUri.Should().Contain("&offset=200&");
        }

        [Test]
        public void should_not_get_unlimited_pages()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetAllTiers().First().Take(500).ToList();

            pages.Count.Should().BeLessThan(500);
        }

        [Test]
        public void should_use_only_absolute_numbering_for_anime_search()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("rid=10&q=100");
            pages[1].Url.FullUri.Should().Contain("q=Monkey%20Island+100");
        }

        [Test]
        public void should_also_use_standard_numbering_for_anime_search()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetTier(0).Should().HaveCount(4);
            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("rid=10&q=100");
            pages[1].Url.FullUri.Should().Contain("rid=10&season=5&ep=4");
            pages[2].Url.FullUri.Should().Contain("q=Monkey%20Island+100");
            pages[3].Url.FullUri.Should().Contain("q=Monkey%20Island&season=5&ep=4");
        }

        [Test]
        public void should_search_by_standard_season_number()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(2);
            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("rid=10&season=3");
            pages[1].Url.FullUri.Should().Contain("q=Monkey%20Island&season=3");
        }

        [Test]
        public void should_search_season_title_alias_without_the_season_parameter()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            _animeSeasonSearchCriteria.SeasonSceneTitles = new List<string> { "Monkey Island" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            var pages = results.GetAllTiers().Select(t => t.First().Url.FullUri).ToList();

            pages.Should().ContainSingle(p => p.Contains("q="));
            pages.Should().Contain(p => p.Contains("?t=search&") && p.Contains("q=Monkey%20Island") && !p.Contains("season="));
        }

        [Test]
        public void should_search_season_title_alias_when_standard_format_search_is_disabled()
        {
            Subject.Settings.AnimeStandardFormatSearch = false;
            _animeSeasonSearchCriteria.SeasonSceneTitles = new List<string> { "Monkey Island" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            var pages = results.GetAllTiers().Select(t => t.First().Url.FullUri).ToList();

            pages.Should().ContainSingle();
            pages.Should().Contain(p => p.Contains("q=Monkey%20Island") && !p.Contains("season="));
        }

        [Test]
        public void should_only_search_the_season_title_without_the_season_parameter()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            _animeSeasonSearchCriteria.SceneTitles = new List<string> { "Monkey Island", "Monkey Island Revenge" };
            _animeSeasonSearchCriteria.SeasonSceneTitles = new List<string> { "Monkey Island Revenge" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            var pages = results.GetAllTiers().Select(t => t.First().Url.FullUri).ToList();

            pages.Should().Contain(p => p.Contains("q=Monkey%20Island&season=3"));
            pages.Should().Contain(p => p.Contains("q=Monkey%20Island%20Revenge") && !p.Contains("season="));
        }

        [Test]
        public void should_not_search_by_rid_if_not_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().NotContain("rid=10");
            page.Url.Query.Should().Contain("q=Monkey");
        }

        [Test]
        public void should_search_by_rid_if_supported()
        {
            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("rid=10");
        }

        [Test]
        public void should_not_search_by_tvdbid_if_not_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().NotContain("rid=10");
            page.Url.Query.Should().Contain("q=Monkey");
        }

        [Test]
        public void should_search_by_tvdbid_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
        }

        [Test]
        public void should_search_by_tvmaze_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvmazeid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvmazeid=30");
        }

        [Test]
        public void should_search_by_imdbid_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "imdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("imdbid=t40");
        }

        [Test]
        public void should_search_by_tmdb_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tmdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tmdbid=50");
        }

        [Test]
        public void should_prefer_search_by_tvdbid_if_rid_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
            page.Url.Query.Should().NotContain("rid=10");
        }

        [Test]
        public void should_use_aggregrated_id_search_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
            page.Url.Query.Should().Contain("rid=10");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_if_no_ids_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_if_no_ids_are_known()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            _singleEpisodeSearchCriteria.Series.TvRageId = 0;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_fallback_to_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "title", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().NotContain("q=");
            pageTier2.Url.Query.Should().Contain("title=Monkey%20Island");
        }

        [Test]
        public void should_url_encode_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "title", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            _singleEpisodeSearchCriteria.SceneTitles[0] = "Elith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().NotContain("q=");
            pageTier2.Url.Query.Should().Contain("title=Elith%20%26%20Little");
        }

        [Test]
        public void should_use_the_passed_categories_for_title_searches()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "title", "season", "ep" };
            _singleEpisodeSearchCriteria.SearchMode = SearchMode.SearchTitle;
            _singleEpisodeSearchCriteria.Series.SeriesType = Tv.SeriesTypes.Anime;
            Subject.Settings.Categories = new[] { 1, 2 };
            Subject.Settings.AnimeCategories = new[] { 7, 8 };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("&cat=7,8&");
            page.Url.Query.Should().NotContain("&cat=1,2&");
        }

        [Test]
        public void should_fallback_to_q()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_encode_raw_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.TvTextSearchEngine = "raw";
            _singleEpisodeSearchCriteria.SceneTitles[0] = "Edith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);

            var pageTier = results.GetTier(0).First().First();

            pageTier.Url.Query.Should().Contain("q=Edith%20%26%20Little");
            pageTier.Url.Query.Should().NotContain(" & ");
            pageTier.Url.Query.Should().Contain("%26");
        }

        [Test]
        public void should_use_clean_title_and_encode()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.TvTextSearchEngine = "sphinx";
            _singleEpisodeSearchCriteria.SceneTitles[0] = "Edith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);

            var pageTier = results.GetTier(0).First().First();

            pageTier.Url.Query.Should().Contain("q=Edith%20and%20Little");
            pageTier.Url.Query.Should().Contain("and");
            pageTier.Url.Query.Should().NotContain(" & ");
            pageTier.Url.Query.Should().NotContain("%26");
        }

        [Test]
        public void should_allow_season_search_even_if_episode_search_is_not_allowed()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "season" };

            var results = Subject.GetSearchRequests(_seasonSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
        }

        [Test]
        public void should_not_allow_season_search_if_season_param_is_not_allowed()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid" };

            var results = Subject.GetSearchRequests(_seasonSearchCriteria);
            results.GetTier(0).Should().HaveCount(0);
        }

        [Test]
        public void should_use_2xxx_categories_from_categories_for_movie_search()
        {
            Subject.Settings.Categories = new[] { 2040 };
            Subject.Settings.AnimeCategories = System.Array.Empty<int>();
            Subject.Settings.MovieCategories = System.Array.Empty<int>();

            _capabilities.SupportedMovieSearchParameters = new[] { "q", "imdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.FullUri.Should().Contain("?t=movie&");
            page.Url.Query.Should().Contain("&cat=2040&");
        }

        [Test]
        public void should_use_movie_feed_for_movie_only_categories()
        {
            Subject.Settings.Categories = new[] { 2040 };
            Subject.Settings.AnimeCategories = System.Array.Empty<int>();
            Subject.Settings.MovieCategories = System.Array.Empty<int>();

            var results = Subject.GetRecentRequests();
            var requests = results.GetAllTiers().SelectMany(t => t).Select(r => r.Url.FullUri).ToList();

            requests.Should().Contain(u => u.Contains("?t=movie&") && u.Contains("&cat=2040&"));
            requests.Should().NotContain(u => u.Contains("t=tvsearch"));
        }

        [Test]
        public void should_not_search_tv_with_movie_only_categories()
        {
            Subject.Settings.Categories = new[] { 2040 };
            Subject.Settings.AnimeCategories = System.Array.Empty<int>();
            Subject.Settings.MovieCategories = System.Array.Empty<int>();

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            results.GetAllTiers().SelectMany(t => t).Should().BeEmpty();
        }

        [Test]
        public void should_emit_movie_query_with_default_categories_for_sonarr_shaped_categories()
        {
            Subject.Settings.Categories = new[] { 5030, 5040 };
            Subject.Settings.AnimeCategories = System.Array.Empty<int>();
            Subject.Settings.MovieCategories = System.Array.Empty<int>();

            _capabilities.SupportedMovieSearchParameters = new[] { "q", "imdbid" };

            var results = Subject.GetSearchRequests(_movieSearchCriteria);
            var requests = results.GetAllTiers().SelectMany(t => t).Select(r => r.Url.FullUri).ToList();

            requests.Should().Contain(u => u.Contains("t=movie") && u.Contains("cat=2000,2010,2020,2030,2040,2045,2050,2060"));
        }

        [Test]
        public void should_emit_movie_feed_with_default_categories_for_sonarr_shaped_categories()
        {
            Subject.Settings.Categories = new[] { 5030, 5040 };
            Subject.Settings.AnimeCategories = System.Array.Empty<int>();
            Subject.Settings.MovieCategories = System.Array.Empty<int>();

            var results = Subject.GetRecentRequests();
            var requests = results.GetAllTiers().SelectMany(t => t).Select(r => r.Url.FullUri).ToList();

            requests.Should().Contain(u => u.Contains("t=tvsearch") && u.Contains("cat=5030,5040"));
            requests.Should().Contain(u => u.Contains("t=movie") && u.Contains("cat=2000,2010,2020,2030,2040,2045,2050,2060"));
        }
    }
}
