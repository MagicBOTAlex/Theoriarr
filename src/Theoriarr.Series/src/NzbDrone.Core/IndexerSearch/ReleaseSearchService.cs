using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchForReleases
    {
        // Movies (Radarr)
        Task<List<DownloadDecision>> MovieSearch(int movieId, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> MovieSearch(Movie movie, bool userInvokedSearch, bool interactiveSearch);

        // Series (Sonarr)
        Task<List<DownloadDecision>> EpisodeSearch(int episodeId, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> EpisodeSearch(Episode episode, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> SeasonSearch(int seriesId, int seasonNumber, bool missingOnly, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> SeasonSearch(int seriesId, int seasonNumber, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch);

        // Runs the search in the background and returns the results gathered so far, so the
        // client can show partial results while the remaining indexers keep searching. A
        // completed search is stored against cacheKey so a later search can reuse it.
        List<DownloadDecision> StartInteractiveSearch(string searchId, string cacheKey, ReleaseSearchDomain domain, Func<Task<List<DownloadDecision>>> search);

        // Aborts a background interactive search (and its in-flight indexer requests) when the
        // client is no longer interested in the results.
        void CancelInteractiveSearch(string searchId);
    }

    public class ReleaseSearchService : ISearchForReleases
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly ISceneMappingService _sceneMapping;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMovieService _movieService;
        private readonly IMovieTranslationService _movieTranslationService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly IEventAggregator _eventAggregator;
        private readonly IReleaseSearchTracker _searchTracker;
        private readonly IReleaseSearchCache _searchCache;
        private readonly Logger _logger;

        public ReleaseSearchService(IIndexerFactory indexerFactory,
                                ISceneMappingService sceneMapping,
                                ISeriesService seriesService,
                                IEpisodeService episodeService,
                                IMovieService movieService,
                                IMovieTranslationService movieTranslationService,
                                IQualityProfileService qualityProfileService,
                                IMakeDownloadDecision makeDownloadDecision,
                                IEventAggregator eventAggregator,
                                IReleaseSearchTracker searchTracker,
                                IReleaseSearchCache searchCache,
                                Logger logger)
        {
            _indexerFactory = indexerFactory;
            _sceneMapping = sceneMapping;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _movieService = movieService;
            _movieTranslationService = movieTranslationService;
            _qualityProfileService = qualityProfileService;
            _makeDownloadDecision = makeDownloadDecision;
            _eventAggregator = eventAggregator;
            _searchTracker = searchTracker;
            _searchCache = searchCache;
            _logger = logger;
        }

        public List<DownloadDecision> StartInteractiveSearch(string searchId, string cacheKey, ReleaseSearchDomain domain, Func<Task<List<DownloadDecision>>> search)
        {
            if (_searchTracker.TryBegin(searchId))
            {
                var token = _searchTracker.GetToken(searchId);

                Task.Run(async () =>
                {
                    using (ReleaseSearchProgressContext.Begin(searchId, token))
                    {
                        try
                        {
                            var decisions = await search();
                            _searchTracker.Complete(searchId, decisions);

                            if (!token.IsCancellationRequested)
                            {
                                _searchCache.Store(cacheKey, decisions);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!token.IsCancellationRequested)
                            {
                                _logger.Error(ex, "Interactive search failed");
                            }

                            _searchTracker.Complete(searchId, null);
                        }
                        finally
                        {
                            if (!token.IsCancellationRequested)
                            {
                                PublishSearchProgress(new ReleaseSearchProgressEvent
                                {
                                    SearchId = searchId,
                                    Domain = domain,
                                    Status = ReleaseSearchProgressStatus.Completed
                                });
                            }
                        }
                    }
                });
            }

            return _searchTracker.GetDecisions(searchId);
        }

        public void CancelInteractiveSearch(string searchId)
        {
            _searchTracker.Cancel(searchId);
        }

        public async Task<List<DownloadDecision>> MovieSearch(int movieId, bool userInvokedSearch, bool interactiveSearch)
        {
            var movie = _movieService.GetMovie(movieId);
            movie.MovieMetadata.Value.Translations = _movieTranslationService.GetAllTranslationsForMovieMetadata(movie.MovieMetadataId);

            return await MovieSearch(movie, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> MovieSearch(Movie movie, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var searchSpec = Get<MovieSearchCriteria>(movie, userInvokedSearch, interactiveSearch);

            var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        private TSpec Get<TSpec>(Movie movie, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec
            {
                Movie = movie,
                UserInvokedSearch = userInvokedSearch,
                InteractiveSearch = interactiveSearch
            };

            var wantedLanguages = _qualityProfileService.GetAcceptableLanguages(movie.QualityProfileId);
            var translations = _movieTranslationService.GetAllTranslationsForMovieMetadata(movie.MovieMetadataId);

            var queryTranslations = new List<string>
            {
                movie.MovieMetadata.Value.Title,
                movie.MovieMetadata.Value.OriginalTitle
            };

            // Add Translation of wanted languages to search query
            foreach (var translation in translations.Where(a => wantedLanguages.Contains(a.Language)))
            {
                queryTranslations.Add(translation.Title);
            }

            spec.SceneTitles = queryTranslations.Where(t => t.IsNotNullOrWhiteSpace()).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            return spec;
        }

        public async Task<List<DownloadDecision>> EpisodeSearch(int episodeId, bool userInvokedSearch, bool interactiveSearch)
        {
            var episode = _episodeService.GetEpisode(episodeId);

            return await EpisodeSearch(episode, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> EpisodeSearch(Episode episode, bool userInvokedSearch, bool interactiveSearch)
        {
            var series = _seriesService.GetSeries(episode.SeriesId);

            if (series.SeriesType == SeriesTypes.Daily)
            {
                if (string.IsNullOrWhiteSpace(episode.AirDate))
                {
                    _logger.Error("Daily episode is missing an air date. Try refreshing the series info.");
                    throw new SearchFailedException("Air date is missing");
                }

                return await SearchDaily(series, episode, false, userInvokedSearch, interactiveSearch);
            }

            if (series.SeriesType == SeriesTypes.Anime)
            {
                if (episode.SeasonNumber == 0 &&
                    episode.SceneAbsoluteEpisodeNumber == null &&
                    episode.AbsoluteEpisodeNumber == null)
                {
                    // Search for special episodes in season 0 that don't have absolute episode numbers
                    return await SearchSpecial(series, new List<Episode> { episode }, false, userInvokedSearch, interactiveSearch);
                }

                return await SearchAnime(series, episode, false, userInvokedSearch, interactiveSearch);
            }

            if (episode.SeasonNumber == 0)
            {
                // Search for special episodes in season 0
                return await SearchSpecial(series, new List<Episode> { episode }, false, userInvokedSearch, interactiveSearch);
            }

            return await SearchSingle(series, episode, false, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> SeasonSearch(int seriesId, int seasonNumber, bool missingOnly, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var episodes = _episodeService.GetEpisodesBySeason(seriesId, seasonNumber);

            if (missingOnly)
            {
                episodes = episodes.Where(e => !e.HasFile).ToList();
            }

            if (episodes.Count == 0)
            {
                _logger.Debug("No wanted episodes found for season {0}", seasonNumber);
                return new List<DownloadDecision>();
            }

            return await SeasonSearch(seriesId, seasonNumber, episodes, monitoredOnly, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> SeasonSearch(int seriesId, int seasonNumber, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var series = _seriesService.GetSeries(seriesId);

            if (series.SeriesType == SeriesTypes.Anime)
            {
                return await SearchAnimeSeason(series, episodes, monitoredOnly, userInvokedSearch, interactiveSearch);
            }

            if (series.SeriesType == SeriesTypes.Daily)
            {
                return await SearchDailySeason(series, episodes, monitoredOnly, userInvokedSearch, interactiveSearch);
            }

            var mappings = GetSceneSeasonMappings(series, episodes);

            var downloadDecisions = new List<DownloadDecision>();

            foreach (var mapping in mappings)
            {
                if (mapping.SeasonNumber == 0)
                {
                    // search for special episodes in season 0
                    downloadDecisions.AddRange(await SearchSpecial(series, mapping.Episodes, monitoredOnly, userInvokedSearch, interactiveSearch));
                    continue;
                }

                if (mapping.Episodes.Count == 1)
                {
                    var searchSpec = Get<SingleEpisodeSearchCriteria>(series, mapping, monitoredOnly, userInvokedSearch, interactiveSearch);
                    searchSpec.SeasonNumber = mapping.SeasonNumber;
                    searchSpec.EpisodeNumber = mapping.EpisodeMapping.EpisodeNumber;

                    var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                    downloadDecisions.AddRange(decisions);
                }
                else
                {
                    var searchSpec = Get<SeasonSearchCriteria>(series, mapping, monitoredOnly, userInvokedSearch, interactiveSearch);
                    searchSpec.SeasonNumber = mapping.SeasonNumber;

                    var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                    downloadDecisions.AddRange(decisions);
                }
            }

            return DeDupeDecisions(downloadDecisions);
        }

        private List<SceneSeasonMapping> GetSceneSeasonMappings(Series series, List<Episode> episodes)
        {
            var dict = new Dictionary<SceneSeasonMapping, SceneSeasonMapping>();

            var sceneMappings = _sceneMapping.FindByTvdbId(series.TvdbId);

            // Group the episode by SceneSeasonNumber/SeasonNumber, in 99% of cases this will result in 1 groupedEpisode
            var groupedEpisodes = episodes.ToLookup(v => ((v.SceneSeasonNumber ?? v.SeasonNumber) * 100000) + v.SeasonNumber);

            foreach (var groupedEpisode in groupedEpisodes)
            {
                var episodeMappings = GetSceneEpisodeMappings(series, groupedEpisode.First(), sceneMappings);

                foreach (var episodeMapping in episodeMappings)
                {
                    var seasonMapping = new SceneSeasonMapping
                    {
                        Episodes = groupedEpisode.ToList(),
                        EpisodeMapping = episodeMapping,
                        SceneTitles = episodeMapping.SceneTitles,
                        SeasonSceneTitles = episodeMapping.SeasonSceneTitles,
                        SearchMode = episodeMapping.SearchMode,
                        SeasonNumber = episodeMapping.SeasonNumber
                    };

                    if (dict.TryGetValue(seasonMapping, out var existing))
                    {
                        existing.Episodes.AddRange(seasonMapping.Episodes);
                        existing.SceneTitles.AddRange(seasonMapping.SceneTitles);
                        existing.SeasonSceneTitles.AddRange(seasonMapping.SeasonSceneTitles);
                    }
                    else
                    {
                        dict[seasonMapping] = seasonMapping;
                    }
                }
            }

            foreach (var item in dict)
            {
                item.Value.Episodes = item.Value.Episodes.Distinct().ToList();
                item.Value.SceneTitles = item.Value.SceneTitles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
                item.Value.SeasonSceneTitles = item.Value.SeasonSceneTitles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            }

            return dict.Values.ToList();
        }

        private List<SceneEpisodeMapping> GetSceneEpisodeMappings(Series series, Episode episode)
        {
            var dict = new Dictionary<SceneEpisodeMapping, SceneEpisodeMapping>();

            var sceneMappings = _sceneMapping.FindByTvdbId(series.TvdbId);

            var episodeMappings = GetSceneEpisodeMappings(series, episode, sceneMappings);

            foreach (var episodeMapping in episodeMappings)
            {
                if (dict.TryGetValue(episodeMapping, out var existing))
                {
                    existing.SceneTitles.AddRange(episodeMapping.SceneTitles);
                    existing.SeasonSceneTitles.AddRange(episodeMapping.SeasonSceneTitles);
                }
                else
                {
                    dict[episodeMapping] = episodeMapping;
                }
            }

            foreach (var item in dict)
            {
                item.Value.SceneTitles = item.Value.SceneTitles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
                item.Value.SeasonSceneTitles = item.Value.SeasonSceneTitles.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            }

            return dict.Values.ToList();
        }

        private IEnumerable<SceneEpisodeMapping> GetSceneEpisodeMappings(Series series, Episode episode, List<SceneMapping> sceneMappings)
        {
            var includeGlobal = true;

            foreach (var sceneMapping in sceneMappings)
            {
                // There are two kinds of mappings:
                // - Mapped on Release Season Number with sceneMapping.SceneSeasonNumber specified and optionally sceneMapping.SeasonNumber. This translates via episode.SceneSeasonNumber/SeasonNumber to specific episodes.
                // - Mapped on Episode Season Number with optionally sceneMapping.SeasonNumber. This translates from episode.SceneSeasonNumber/SeasonNumber to specific releases. (Filter by episode.SeasonNumber or globally)

                var ignoreSceneNumbering = sceneMapping.SceneOrigin == "tvdb" || sceneMapping.SceneOrigin == "unknown:tvdb";
                var mappingSceneSeasonNumber = sceneMapping.SceneSeasonNumber.NonNegative();
                var mappingSeasonNumber = sceneMapping.SeasonNumber.NonNegative();

                // Select scene or tvdb on the episode
                var mappedSeasonNumber = ignoreSceneNumbering ? episode.SeasonNumber : (episode.SceneSeasonNumber ?? episode.SeasonNumber);
                var releaseSeasonNumber = sceneMapping.SceneSeasonNumber.NonNegative() ?? mappedSeasonNumber;

                if (mappingSceneSeasonNumber.HasValue)
                {
                    // Apply the alternative mapping (release to scene/tvdb)
                    var mappedAltSeasonNumber = sceneMapping.SeasonNumber.NonNegative() ?? sceneMapping.SceneSeasonNumber.NonNegative() ?? mappedSeasonNumber;

                    // Check if the mapping applies to the current season
                    if (mappedAltSeasonNumber != mappedSeasonNumber)
                    {
                        continue;
                    }
                }
                else
                {
                    // Check if the mapping applies to the current season
                    if (mappingSeasonNumber.HasValue && mappingSeasonNumber.Value != episode.SeasonNumber)
                    {
                        continue;
                    }
                }

                if (sceneMapping.SearchTerm == series.Title && sceneMapping.FilterRegex.IsNullOrWhiteSpace())
                {
                    // Disable the implied mapping if we have an explicit mapping by the same name
                    includeGlobal = false;
                }

                var isSeasonTitle = mappingSceneSeasonNumber.HasValue && series.CleanTitle != sceneMapping.SearchTerm.CleanSeriesTitle();

                // By default we do a alt title search in case indexers don't have the release properly indexed.  Services can override this behavior.
                var searchMode = sceneMapping.SearchMode ?? (isSeasonTitle ? SearchMode.SearchTitle : SearchMode.Default);

                var seasonSceneTitles = isSeasonTitle ? new List<string> { sceneMapping.SearchTerm } : new List<string>();

                if (ignoreSceneNumbering)
                {
                    yield return new SceneEpisodeMapping
                    {
                        Episode = episode,
                        SearchMode = searchMode,
                        SceneTitles = new List<string> { sceneMapping.SearchTerm },
                        SeasonSceneTitles = seasonSceneTitles,
                        SeasonNumber = releaseSeasonNumber,
                        EpisodeNumber = episode.EpisodeNumber,
                        AbsoluteEpisodeNumber = episode.AbsoluteEpisodeNumber
                    };
                }
                else
                {
                    yield return new SceneEpisodeMapping
                    {
                        Episode = episode,
                        SearchMode = searchMode,
                        SceneTitles = new List<string> { sceneMapping.SearchTerm },
                        SeasonSceneTitles = seasonSceneTitles,
                        SeasonNumber = releaseSeasonNumber,
                        EpisodeNumber = episode.SceneEpisodeNumber ?? episode.EpisodeNumber,
                        AbsoluteEpisodeNumber = episode.SceneAbsoluteEpisodeNumber ?? episode.AbsoluteEpisodeNumber
                    };
                }
            }

            if (includeGlobal)
            {
                yield return new SceneEpisodeMapping
                {
                    Episode = episode,
                    SearchMode = SearchMode.Default,
                    SceneTitles = new List<string> { series.Title },
                    SeasonSceneTitles = new List<string>(),
                    SeasonNumber = episode.SceneSeasonNumber ?? episode.SeasonNumber,
                    EpisodeNumber = episode.SceneEpisodeNumber ?? episode.EpisodeNumber,
                    AbsoluteEpisodeNumber = episode.SceneSeasonNumber ?? episode.AbsoluteEpisodeNumber
                };
            }
        }

        private async Task<List<DownloadDecision>> SearchSingle(Series series, Episode episode, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var mappings = GetSceneEpisodeMappings(series, episode);

            var downloadDecisions = new List<DownloadDecision>();

            foreach (var mapping in mappings)
            {
                var searchSpec = Get<SingleEpisodeSearchCriteria>(series, mapping, monitoredOnly, userInvokedSearch, interactiveSearch);
                searchSpec.SeasonNumber = mapping.SeasonNumber;
                searchSpec.EpisodeNumber = mapping.EpisodeNumber;

                var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                downloadDecisions.AddRange(decisions);
            }

            return DeDupeDecisions(downloadDecisions);
        }

        private async Task<List<DownloadDecision>> SearchDaily(Series series, Episode episode, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var airDate = DateTime.ParseExact(episode.AirDate, Episode.AIR_DATE_FORMAT, CultureInfo.InvariantCulture);
            var searchSpec = Get<DailyEpisodeSearchCriteria>(series, new List<Episode> { episode }, monitoredOnly, userInvokedSearch, interactiveSearch);
            searchSpec.AirDate = airDate;

            var downloadDecisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);

            return DeDupeDecisions(downloadDecisions);
        }

        private async Task<List<DownloadDecision>> SearchAnime(Series series, Episode episode, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch, bool isSeasonSearch = false)
        {
            var searchSpec = Get<AnimeEpisodeSearchCriteria>(series, new List<Episode> { episode }, monitoredOnly, userInvokedSearch, interactiveSearch);

            searchSpec.IsSeasonSearch = isSeasonSearch;

            searchSpec.SeasonNumber = episode.SceneSeasonNumber ?? episode.SeasonNumber;
            searchSpec.EpisodeNumber = episode.SceneEpisodeNumber ?? episode.EpisodeNumber;
            searchSpec.AbsoluteEpisodeNumber = episode.SceneAbsoluteEpisodeNumber ?? episode.AbsoluteEpisodeNumber ?? 0;

            var downloadDecisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);

            return DeDupeDecisions(downloadDecisions);
        }

        private async Task<List<DownloadDecision>> SearchSpecial(Series series, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var searchSpec = Get<SpecialEpisodeSearchCriteria>(series, episodes, monitoredOnly, userInvokedSearch, interactiveSearch);

            // build list of queries for each episode in the form: "<series> <episode-title>"
            searchSpec.EpisodeQueryTitles = episodes.Where(e => !string.IsNullOrWhiteSpace(e.Title))
                                                    .Where(e => interactiveSearch || !monitoredOnly || e.Monitored)
                                                    .SelectMany(e => searchSpec.CleanSceneTitles.Select(title => title + " " + SearchCriteriaBase.GetCleanSceneTitle(e.Title)))
                                                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                                                    .ToArray();

            downloadDecisions.AddRange(await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec));

            // Search for each episode by season/episode number as well
            foreach (var episode in episodes)
            {
                // Episode needs to be monitored if it's not an interactive search
                if (!interactiveSearch && monitoredOnly && !episode.Monitored)
                {
                    continue;
                }

                downloadDecisions.AddRange(await SearchSingle(series, episode, monitoredOnly, userInvokedSearch, interactiveSearch));
            }

            return DeDupeDecisions(downloadDecisions);
        }

        private async Task<List<DownloadDecision>> SearchAnimeSeason(Series series, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            // Episode needs to be monitored if it's not an interactive search
            // and Ensure episode has an airdate and has already aired
            var episodesToSearch = episodes
                .Where(ep => interactiveSearch || !monitoredOnly || ep.Monitored)
                .Where(ep => ep.AirDateUtc.HasValue && ep.AirDateUtc.Value.Before(DateTime.UtcNow))
                .ToList();

            var seasonEpisodes = episodes.Select(e => e.SeasonNumber).Distinct()
                .SelectMany(seasonNumber => _episodeService.GetEpisodesBySeason(series.Id, seasonNumber))
                .ToList();

            var allEpisodesAiredOrAiringSoon = seasonEpisodes.All(ep => ep.AirDateUtc.HasValue && !ep.AirDateUtc.Value.After(DateTime.UtcNow.AddHours(24)));

            var seasonMappings = GetSceneSeasonMappings(series, episodesToSearch);

            var seasonsToSearch = seasonMappings
                .GroupBy(ep => ep.SeasonNumber)
                .Select(epList => epList.First())
                .ToList();

            foreach (var season in seasonsToSearch)
            {
                var searchSpec = Get<AnimeSeasonSearchCriteria>(series, season, monitoredOnly, userInvokedSearch, interactiveSearch);
                searchSpec.SeasonNumber = season.SeasonNumber;

                searchSpec.SceneTitles = seasonMappings
                    .Where(m => m.SeasonNumber == season.SeasonNumber)
                    .SelectMany(m => m.SceneTitles)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                searchSpec.SeasonSceneTitles = seasonMappings
                    .Where(m => m.SeasonNumber == season.SeasonNumber)
                    .SelectMany(m => m.SeasonSceneTitles)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                downloadDecisions.AddRange(decisions);
            }

            foreach (var episode in episodesToSearch.Where(ep => !allEpisodesAiredOrAiringSoon))
            {
                downloadDecisions.AddRange(await SearchAnime(series, episode, monitoredOnly, userInvokedSearch, interactiveSearch, true));
            }

            return DeDupeDecisions(downloadDecisions);
        }

        private async Task<List<DownloadDecision>> SearchDailySeason(Series series, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            // Episode needs to be monitored if it's not an interactive search
            // and Ensure episode has an airdate
            var episodesToSearch = episodes
                .Where(ep => interactiveSearch || !monitoredOnly || ep.Monitored)
                .Where(ep => ep.AirDate.IsNotNullOrWhiteSpace())
                .ToList();

            foreach (var yearGroup in episodesToSearch.GroupBy(v => DateTime.ParseExact(v.AirDate, Episode.AIR_DATE_FORMAT, CultureInfo.InvariantCulture).Year))
            {
                var yearEpisodes = yearGroup.ToList();

                if (yearEpisodes.Count > 1)
                {
                    var searchSpec = Get<DailySeasonSearchCriteria>(series, yearEpisodes, monitoredOnly, userInvokedSearch, interactiveSearch);
                    searchSpec.Year = yearGroup.Key;

                    downloadDecisions.AddRange(await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec));
                }
                else
                {
                    downloadDecisions.AddRange(await SearchDaily(series, yearEpisodes.First(), monitoredOnly, userInvokedSearch, interactiveSearch));
                }
            }

            return DeDupeDecisions(downloadDecisions);
        }

        private TSpec Get<TSpec>(Series series, List<Episode> episodes, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Series = series;
            spec.SceneTitles = _sceneMapping.GetSceneNames(series.TvdbId,
                                                           episodes.Select(e => e.SeasonNumber).Distinct().ToList(),
                                                           episodes.Select(e => e.SceneSeasonNumber ?? e.SeasonNumber).Distinct().ToList());

            spec.Episodes = episodes;
            spec.MonitoredEpisodesOnly = monitoredOnly;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            if (!spec.SceneTitles.Contains(series.Title, StringComparer.InvariantCultureIgnoreCase))
            {
                spec.SceneTitles.Add(series.Title);
            }

            return spec;
        }

        private TSpec Get<TSpec>(Series series, SceneEpisodeMapping mapping, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Series = series;
            spec.SceneTitles = mapping.SceneTitles;
            spec.SearchMode = mapping.SearchMode;

            spec.Episodes = new List<Episode> { mapping.Episode };
            spec.MonitoredEpisodesOnly = monitoredOnly;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private TSpec Get<TSpec>(Series series, SceneSeasonMapping mapping, bool monitoredOnly, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Series = series;
            spec.SceneTitles = mapping.SceneTitles;
            spec.SearchMode = mapping.SearchMode;

            spec.Episodes = mapping.Episodes;
            spec.MonitoredEpisodesOnly = monitoredOnly;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private async Task<List<DownloadDecision>> Dispatch(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, SearchCriteriaBase criteriaBase)
        {
            var indexers = criteriaBase.InteractiveSearch ?
                _indexerFactory.InteractiveSearchEnabled() :
                _indexerFactory.AutomaticSearchEnabled();

            // Filter indexers to untagged indexers and indexers with intersecting tags
            if (criteriaBase is MovieSearchCriteria movieCriteria)
            {
                indexers = indexers.Where(i => i.Definition.Tags.Empty() || i.Definition.Tags.Intersect(movieCriteria.Movie.Tags).Any()).ToList();
            }
            else
            {
                indexers = indexers.Where(i => i.Definition.Tags.Empty() || i.Definition.Tags.Intersect(criteriaBase.Series.Tags).Any()).ToList();
            }

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", criteriaBase, indexers.Count);

            var searchId = ReleaseSearchProgressContext.SearchId;
            var reportProgress = criteriaBase.InteractiveSearch && searchId.IsNotNullOrWhiteSpace();
            var domain = criteriaBase is MovieSearchCriteria ? ReleaseSearchDomain.Movies : ReleaseSearchDomain.Series;

            if (reportProgress)
            {
                PublishSearchProgress(new ReleaseSearchProgressEvent
                {
                    SearchId = searchId,
                    Domain = domain,
                    Status = ReleaseSearchProgressStatus.Started,
                    Indexers = indexers
                        .Select(indexer => new ReleaseSearchIndexerProgress { Id = indexer.Definition.Id, Name = indexer.Definition.Name })
                        .ToList()
                });
            }

            var tasks = indexers.Select(indexer => DispatchIndexer(searchAction, indexer, criteriaBase, searchId, domain, reportProgress));

            var batch = await Task.WhenAll(tasks);

            var reports = batch.SelectMany(x => x.Releases).ToList();

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", reports.Count, criteriaBase, indexers.Count);

            // Update the last search time if at least 1 indexer was searched.
            if (indexers.Any())
            {
                var lastSearchTime = DateTime.UtcNow;
                _logger.Debug("Setting last search time to: {0}", lastSearchTime);

                if (criteriaBase is MovieSearchCriteria searchedMovie)
                {
                    searchedMovie.Movie.LastSearchTime = lastSearchTime;
                    _movieService.UpdateLastSearchTime(searchedMovie.Movie);
                }
                else
                {
                    criteriaBase.Episodes.ForEach(e => e.LastSearchTime = lastSearchTime);
                    _episodeService.UpdateLastSearchTime(criteriaBase.Episodes);
                }
            }

            if (reportProgress)
            {
                // Decisions were already made per indexer as each one finished.
                return batch.SelectMany(x => x.Decisions ?? new List<DownloadDecision>()).ToList();
            }

            return _makeDownloadDecision.GetSearchDecision(reports, criteriaBase).ToList();
        }

        private async Task<IndexerSearchReport> DispatchIndexer(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, IIndexer indexer, SearchCriteriaBase criteriaBase, string searchId, ReleaseSearchDomain domain, bool reportProgress)
        {
            var failed = false;
            IList<ReleaseInfo> releases;

            try
            {
                releases = await searchAction(indexer);
            }
            catch (Exception ex)
            {
                failed = true;
                releases = Array.Empty<ReleaseInfo>();

                if (!ReleaseSearchProgressContext.Token.IsCancellationRequested)
                {
                    _logger.Error(ex, "Error while searching for {0}", criteriaBase);
                }
            }

            List<DownloadDecision> decisions = null;

            if (reportProgress && !ReleaseSearchProgressContext.Token.IsCancellationRequested)
            {
                decisions = _makeDownloadDecision.GetSearchDecision(releases.ToList(), criteriaBase).ToList();

                _searchTracker.RecordIndexer(searchId, decisions);

                PublishSearchProgress(new ReleaseSearchProgressEvent
                {
                    SearchId = searchId,
                    Domain = domain,
                    Status = ReleaseSearchProgressStatus.IndexerCompleted,
                    IndexerId = indexer.Definition.Id,
                    IndexerName = indexer.Definition.Name,
                    ReleaseCount = releases.Count,
                    Failed = failed
                });
            }

            return new IndexerSearchReport
            {
                Releases = releases,
                Decisions = decisions
            };
        }

        private void PublishSearchProgress(ReleaseSearchProgressEvent message)
        {
            try
            {
                _eventAggregator.PublishEvent(message);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to publish release search progress for {0}", message.SearchId);
            }
        }

        private List<DownloadDecision> DeDupeDecisions(List<DownloadDecision> decisions)
        {
            // De-dupe reports by guid so duplicate results aren't returned. Pick the one with the least rejections and higher indexer priority.
            // Search criteria are homogeneous per dispatch (all movie or all episode), so the
            // release used for grouping is always the same kind within one call.
            return decisions.GroupBy(d => GetRelease(d).Guid)
                .Select(d => d.OrderBy(v => v.Rejections.Count()).ThenBy(v => GetRelease(v)?.IndexerPriority ?? IndexerDefinition.DefaultPriority).First())
                .ToList();
        }

        private static ReleaseInfo GetRelease(DownloadDecision decision)
        {
            return decision.RemoteEpisode?.Release ?? decision.RemoteMovie?.Release;
        }

        private class IndexerSearchReport
        {
            public IList<ReleaseInfo> Releases { get; set; }
            public List<DownloadDecision> Decisions { get; set; }
        }
    }
}
