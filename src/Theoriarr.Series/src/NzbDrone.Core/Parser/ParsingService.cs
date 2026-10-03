using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Parser.RomanNumerals;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public interface IParsingService
    {
        Series GetSeries(string title);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, int tvRageId, string imdbId, SearchCriteriaBase searchCriteria = null);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, Series series);
        RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int seriesId, IEnumerable<int> episodeIds);
        List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool sceneSource, SearchCriteriaBase searchCriteria = null);
        ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, int tvdbId, int tvRageId, string imdbId, SearchCriteriaBase searchCriteria = null);
        ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, Series series);

        Movie GetMovie(string title);
        RemoteMovie Map(ParsedMovieInfo parsedMovieInfo, string imdbId, int tmdbId, SearchCriteriaBase searchCriteria = null);
        RemoteMovie Map(ParsedMovieInfo parsedMovieInfo, int movieId);
        ParsedMovieInfo ParseMinimalPathMovieInfo(string path);
    }

    public class ParsingService : IParsingService
    {
        private static HashSet<ArabicRomanNumeral> _arabicRomanNumeralMappings;

        private readonly IEpisodeService _episodeService;
        private readonly ISeriesService _seriesService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly ISeriesTitleMatcher _seriesTitleMatcher;
        private readonly IMovieService _movieService;
        private readonly Logger _logger;

        public ParsingService(IEpisodeService episodeService,
                              ISeriesService seriesService,
                              ISceneMappingService sceneMappingService,
                              ISeriesTitleMatcher seriesTitleMatcher,
                              IMovieService movieService,
                              Logger logger)
        {
            _episodeService = episodeService;
            _seriesService = seriesService;
            _sceneMappingService = sceneMappingService;
            _seriesTitleMatcher = seriesTitleMatcher;
            _movieService = movieService;
            _logger = logger;

            if (_arabicRomanNumeralMappings == null)
            {
                _arabicRomanNumeralMappings = RomanNumeralParser.GetArabicRomanNumeralsMapping();
            }
        }

        public Series GetSeries(string title)
        {
            var parsedEpisodeInfo = Parser.ParseTitle(title);

            if (parsedEpisodeInfo == null)
            {
                return _seriesService.FindByTitle(title);
            }

            var tvdbId = _sceneMappingService.FindTvdbId(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber ?? -1);

            if (tvdbId.HasValue)
            {
                return _seriesService.FindByTvdbId(tvdbId.Value);
            }

            var series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitle);

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo.AllTitles != null)
            {
                series = GetSeriesByAllTitles(parsedEpisodeInfo);
            }

            if (series == null)
            {
                series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear,
                                                    parsedEpisodeInfo.SeriesTitleInfo.Year);
            }

            return series;
        }

        private Series GetSeriesByAllTitles(ParsedEpisodeInfo parsedEpisodeInfo)
        {
            var year = parsedEpisodeInfo.SeriesTitleInfo.Year;
            Series foundSeries = null;
            int? foundTvdbId = null;

            // Match each title individually, they must all resolve to the same tvdbid
            foreach (var title in parsedEpisodeInfo.SeriesTitleInfo.AllTitles)
            {
                Series series = null;

                if (year > 0)
                {
                    series = _seriesService.FindByTitle(title, year);

                    // Fall back to title + year being part of the title, this will allow
                    // matching series with the same name that include the year in the title.
                    if (series == null)
                    {
                        series = _seriesService.FindByTitle($"{title} {year}");
                    }
                }
                else
                {
                    series = _seriesService.FindByTitle(title);
                }

                var tvdbId = series?.TvdbId;

                if (series == null)
                {
                    tvdbId = _sceneMappingService.FindTvdbId(title, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber ?? -1);
                }

                if (!tvdbId.HasValue)
                {
                    _logger.Trace("Title {0} not matching any series.", title);
                    continue;
                }

                if (foundTvdbId.HasValue && tvdbId != foundTvdbId)
                {
                    _logger.Trace("Title {0} both matches tvdbid {1} and {2}, no series selected.", parsedEpisodeInfo.SeriesTitle, foundTvdbId, tvdbId);
                    return null;
                }

                if (foundSeries == null)
                {
                    foundSeries = series;
                }

                foundTvdbId = tvdbId;
            }

            if (foundSeries == null && foundTvdbId.HasValue)
            {
                foundSeries = _seriesService.FindByTvdbId(foundTvdbId.Value);
            }

            return foundSeries;
        }

        private Series GetSeriesAliasTitleAndYear(ParsedEpisodeInfo parsedEpisodeInfo)
        {
            var year = parsedEpisodeInfo.SeriesTitleInfo.Year;
            var titleWithoutyear = parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear;
            var tvdbId = _sceneMappingService.FindTvdbId(titleWithoutyear, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber ?? -1);

            if (tvdbId.HasValue)
            {
                var series = _seriesService.FindByTvdbId(tvdbId.Value);

                if (series != null && series.Year == year)
                {
                    return series;
                }
            }

            return null;
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, int tvRageId, string imdbId, SearchCriteriaBase searchCriteria = null)
        {
            return Map(parsedEpisodeInfo, tvdbId, tvRageId, imdbId, null, searchCriteria);
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, Series series)
        {
            return Map(parsedEpisodeInfo, 0, 0, null, series, null);
        }

        public RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int seriesId, IEnumerable<int> episodeIds)
        {
            return new RemoteEpisode
                   {
                       ParsedEpisodeInfo = parsedEpisodeInfo,
                       Series = _seriesService.GetSeries(seriesId),
                       Episodes = _episodeService.GetEpisodes(episodeIds)
                   };
        }

        private RemoteEpisode Map(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, int tvRageId, string imdbId, Series series, SearchCriteriaBase searchCriteria)
        {
            var sceneMapping = _sceneMappingService.FindSceneMapping(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber ?? -1);

            var remoteEpisode = new RemoteEpisode
            {
                ParsedEpisodeInfo = parsedEpisodeInfo,
                SceneMapping = sceneMapping,
                MappedSeasonNumber = parsedEpisodeInfo.SeasonNumber
            };

            // For now we just detect tvdb vs scene, but we can do multiple 'origins' in the future.
            var sceneSource = true;
            if (sceneMapping != null)
            {
                if (sceneMapping.SeasonNumber.HasValue && sceneMapping.SeasonNumber.Value >= 0 &&
                    sceneMapping.SceneSeasonNumber <= parsedEpisodeInfo.SeasonNumber)
                {
                    remoteEpisode.MappedSeasonNumber += sceneMapping.SeasonNumber.Value - sceneMapping.SceneSeasonNumber.Value;
                }

                if (parsedEpisodeInfo.IsSeasonTitle)
                {
                    var mappedSeasonNumber = sceneMapping.SceneSeasonNumber.NonNegative() ?? sceneMapping.SeasonNumber.NonNegative();

                    if (mappedSeasonNumber.HasValue)
                    {
                        // Assign the season number from the mapping so we have a valid season number
                        remoteEpisode.MappedSeasonNumber = mappedSeasonNumber.Value;
                    }
                }

                if (sceneMapping.SceneOrigin == "tvdb")
                {
                    sceneSource = false;
                }
                else if (sceneMapping.Type == "XemService" &&
                         sceneMapping.SceneSeasonNumber.NonNegative().HasValue &&
                         parsedEpisodeInfo.SeasonNumber == 1 &&
                         sceneMapping.SceneSeasonNumber != parsedEpisodeInfo.SeasonNumber)
                {
                    remoteEpisode.MappedSeasonNumber = sceneMapping.SceneSeasonNumber.Value;
                }
            }

            if (series == null)
            {
                var seriesMatch = FindSeries(parsedEpisodeInfo, tvdbId, tvRageId, imdbId, sceneMapping, searchCriteria);

                if (seriesMatch != null)
                {
                    series = seriesMatch.Series;
                    remoteEpisode.SeriesMatchType = seriesMatch.MatchType;
                }
            }

            if (series != null)
            {
                remoteEpisode.Series = series;

                if (remoteEpisode.MappedSeasonNumber.HasValue &&
                    ValidateParsedEpisodeInfo.ValidateForSeriesType(parsedEpisodeInfo, series))
                {
                    remoteEpisode.Episodes = GetEpisodes(parsedEpisodeInfo, series, remoteEpisode.MappedSeasonNumber.Value, sceneSource, searchCriteria);
                }
            }

            remoteEpisode.Languages = parsedEpisodeInfo.Languages;

            if (remoteEpisode.Episodes == null)
            {
                remoteEpisode.Episodes = new List<Episode>();
            }

            if (searchCriteria != null)
            {
                var requestedEpisodes = searchCriteria.Episodes.ToDictionaryIgnoreDuplicates(v => v.Id);
                remoteEpisode.EpisodeRequested = remoteEpisode.Episodes.Any(v => requestedEpisodes.ContainsKey(v.Id));
            }

            return remoteEpisode;
        }

        public List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, bool sceneSource, SearchCriteriaBase searchCriteria = null)
        {
            if (sceneSource)
            {
                var remoteEpisode = Map(parsedEpisodeInfo, 0, 0, null, series, searchCriteria);

                return remoteEpisode.Episodes;
            }

            if (!parsedEpisodeInfo.SeasonNumber.HasValue)
            {
                return new List<Episode>();
            }

            return GetEpisodes(parsedEpisodeInfo, series, parsedEpisodeInfo.SeasonNumber.Value, sceneSource, searchCriteria);
        }

        private List<Episode> GetEpisodes(ParsedEpisodeInfo parsedEpisodeInfo, Series series, int mappedSeasonNumber, bool sceneSource, SearchCriteriaBase searchCriteria)
        {
            if (parsedEpisodeInfo.FullSeason)
            {
                if (series.UseSceneNumbering && sceneSource)
                {
                    var episodes = _episodeService.GetEpisodesBySceneSeason(series.Id, mappedSeasonNumber);

                    // If episodes were found by the scene season number return them, otherwise fallback to look-up by season number
                    if (episodes.Any())
                    {
                        return episodes;
                    }
                }

                return _episodeService.GetEpisodesBySeason(series.Id, mappedSeasonNumber);
            }

            if (parsedEpisodeInfo.IsDaily)
            {
                var episodeInfo = GetDailyEpisode(series, parsedEpisodeInfo.AirDate, parsedEpisodeInfo.DailyPart, searchCriteria);

                if (episodeInfo != null)
                {
                    return new List<Episode> { episodeInfo };
                }

                return new List<Episode>();
            }

            if (parsedEpisodeInfo.IsAbsoluteNumbering)
            {
                return GetAnimeEpisodes(series, parsedEpisodeInfo, mappedSeasonNumber, sceneSource, searchCriteria);
            }

            if (parsedEpisodeInfo.IsPossibleSceneSeasonSpecial)
            {
                var parsedSpecialEpisodeInfo = ParseSpecialEpisodeTitle(parsedEpisodeInfo, parsedEpisodeInfo.ReleaseTitle, series);

                if (parsedSpecialEpisodeInfo != null)
                {
                    // Use the season number and disable scene source since the season/episode numbers that were returned are not scene numbers
                    return GetStandardEpisodes(series, parsedSpecialEpisodeInfo, parsedSpecialEpisodeInfo.SeasonNumber.Value, false, searchCriteria);
                }
            }

            if (parsedEpisodeInfo.Special && mappedSeasonNumber != 0)
            {
                return new List<Episode>();
            }

            return GetStandardEpisodes(series, parsedEpisodeInfo, mappedSeasonNumber, sceneSource, searchCriteria);
        }

        public ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, int tvdbId, int tvRageId, string imdbId, SearchCriteriaBase searchCriteria = null)
        {
            if (searchCriteria != null)
            {
                if (tvdbId != 0 && tvdbId == searchCriteria.Series.TvdbId)
                {
                    return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, searchCriteria.Series);
                }

                if (tvRageId != 0 && tvRageId == searchCriteria.Series.TvRageId)
                {
                    return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, searchCriteria.Series);
                }

                if (imdbId.IsNotNullOrWhiteSpace() && imdbId.Equals(searchCriteria.Series.ImdbId, StringComparison.Ordinal))
                {
                    return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, searchCriteria.Series);
                }
            }

            var series = GetSeries(releaseTitle);

            if (series == null)
            {
                series = _seriesService.FindByTitleInexact(releaseTitle);
            }

            if (series == null && tvdbId > 0)
            {
                series = _seriesService.FindByTvdbId(tvdbId);
            }

            if (series == null && tvRageId > 0)
            {
                series = _seriesService.FindByTvRageId(tvRageId);
            }

            if (series == null && imdbId.IsNotNullOrWhiteSpace())
            {
                series = _seriesService.FindByImdbId(imdbId);
            }

            if (series == null)
            {
                _logger.Debug("No matching series {0}", releaseTitle);
                return null;
            }

            return ParseSpecialEpisodeTitle(parsedEpisodeInfo, releaseTitle, series);
        }

        public ParsedEpisodeInfo ParseSpecialEpisodeTitle(ParsedEpisodeInfo parsedEpisodeInfo, string releaseTitle, Series series)
        {
            // SxxE00 episodes are sometimes mapped via TheXEM, don't use episode title parsing in that case.
            if (parsedEpisodeInfo != null && parsedEpisodeInfo.IsPossibleSceneSeasonSpecial && series.UseSceneNumbering)
            {
                if (_episodeService.FindEpisodesBySceneNumbering(series.Id, parsedEpisodeInfo.SeasonNumber.Value, 0).Any())
                {
                    return parsedEpisodeInfo;
                }
            }

            // find special episode in series season 0
            var episode = _episodeService.FindEpisodeByTitle(series.Id, 0, releaseTitle);

            if (episode != null)
            {
                // create parsed info from tv episode
                var info = new ParsedEpisodeInfo
                {
                    ReleaseTitle = releaseTitle,
                    SeriesTitle = series.Title,
                    SeriesTitleInfo = new SeriesTitleInfo
                        {
                            Title = series.Title
                        },
                    SeasonNumber = episode.SeasonNumber,
                    EpisodeNumbers = new int[1] { episode.EpisodeNumber },
                    FullSeason = false,
                    Quality = QualityParser.ParseQuality(releaseTitle),
                    ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(releaseTitle),
                    Languages = LanguageParser.ParseLanguages(releaseTitle),
                    Special = true
                };

                _logger.Debug("Found special episode {0} for title '{1}'", info, releaseTitle);
                return info;
            }

            return null;
        }

        private FindSeriesResult FindSeries(ParsedEpisodeInfo parsedEpisodeInfo, int tvdbId, int tvRageId, string imdbId, SceneMapping sceneMapping, SearchCriteriaBase searchCriteria)
        {
            Series series = null;

            if (sceneMapping != null)
            {
                if (searchCriteria != null && searchCriteria.Series.TvdbId == sceneMapping.TvdbId)
                {
                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Alias);
                }

                series = _seriesService.FindByTvdbId(sceneMapping.TvdbId);

                if (series == null)
                {
                    _logger.Debug("No matching series {0}", parsedEpisodeInfo.SeriesTitle);
                    return null;
                }

                return new FindSeriesResult(series, SeriesMatchType.Alias);
            }

            if (searchCriteria != null)
            {
                if (searchCriteria.Series.CleanTitle == parsedEpisodeInfo.SeriesTitle.CleanSeriesTitle())
                {
                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Title);
                }

                if (tvdbId > 0 && tvdbId == searchCriteria.Series.TvdbId)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVDB ID {0}, an alias may be needed for: {1}", tvdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvdbId", tvdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvdbIdMatch", tvdbId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Id);
                }

                if (tvRageId > 0 && tvRageId == searchCriteria.Series.TvRageId && tvdbId <= 0)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVRage ID {0}, an alias may be needed for: {1}", tvRageId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvRageId", tvRageId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvRageIdMatch", tvRageId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Id);
                }

                if (imdbId.IsNotNullOrWhiteSpace() && imdbId.Equals(searchCriteria.Series.ImdbId, StringComparison.Ordinal) && tvdbId <= 0)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by IMDb ID {0}, an alias may be needed for: {1}", imdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("ImdbId", imdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("ImdbIdMatch", imdbId, parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    return new FindSeriesResult(searchCriteria.Series, SeriesMatchType.Id);
                }
            }

            var matchType = SeriesMatchType.Unknown;
            series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitle);

            if (series != null)
            {
                matchType = SeriesMatchType.Title;
            }

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo.AllTitles != null)
            {
                series = GetSeriesByAllTitles(parsedEpisodeInfo);
                matchType = SeriesMatchType.Title;
            }

            if (series == null && parsedEpisodeInfo.SeriesTitleInfo.Year > 0)
            {
                series = _seriesService.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear, parsedEpisodeInfo.SeriesTitleInfo.Year);
                matchType = SeriesMatchType.Title;

                if (series == null)
                {
                    series = GetSeriesAliasTitleAndYear(parsedEpisodeInfo);
                    matchType = SeriesMatchType.Alias;
                }
            }

            if (series == null && tvdbId > 0)
            {
                series = _seriesService.FindByTvdbId(tvdbId);

                if (series != null)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVDB ID {0}, an alias may be needed for: {1}", tvdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvdbId", tvdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvdbIdMatch", tvdbId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    matchType = SeriesMatchType.Id;
                }
            }

            if (series == null && tvRageId > 0 && tvdbId <= 0)
            {
                series = _seriesService.FindByTvRageId(tvRageId);

                if (series != null)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by TVRage ID {0}, an alias may be needed for: {1}", tvRageId, parsedEpisodeInfo.SeriesTitle)
                           .Property("TvRageId", tvRageId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("TvRageIdMatch", tvRageId.ToString(), parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    matchType = SeriesMatchType.Id;
                }
            }

            if (series == null && imdbId.IsNotNullOrWhiteSpace() && tvdbId <= 0)
            {
                series = _seriesService.FindByImdbId(imdbId);

                if (series != null)
                {
                    _logger.ForDebugEvent()
                           .Message("Found matching series by IMDb ID {0}, an alias may be needed for: {1}", imdbId, parsedEpisodeInfo.SeriesTitle)
                           .Property("ImdbId", imdbId)
                           .Property("ParsedEpisodeInfo", parsedEpisodeInfo)
                           .WriteSentryWarn("ImdbIdMatch", imdbId, parsedEpisodeInfo.SeriesTitle)
                           .Log();

                    matchType = SeriesMatchType.Id;
                }
            }

            if (series == null)
            {
                series = FindSeriesByFuzzyTitle(parsedEpisodeInfo, searchCriteria);

                if (series != null)
                {
                    matchType = SeriesMatchType.Alias;
                }
            }

            if (series == null)
            {
                _logger.Debug("No matching series {0}", parsedEpisodeInfo.SeriesTitle);
                return null;
            }

            return new FindSeriesResult(series, matchType);
        }

        // Last-resort, fuzzy resolution of alternative names: exact titles, ids and scene mappings
        // run first, so this only fills the gap for releases named with a variant the exact paths
        // cannot see. During a search the match is restricted to the series that was searched, so it
        // cannot steal a release for an unrelated series.
        private Series FindSeriesByFuzzyTitle(ParsedEpisodeInfo parsedEpisodeInfo, SearchCriteriaBase searchCriteria)
        {
            var restrictTo = searchCriteria?.Series;

            var match = _seriesTitleMatcher.Match(parsedEpisodeInfo.SeriesTitle, restrictTo);

            // The raw release title is only a matching source when we already know which series we
            // are looking for (a search). With nothing to restrict to, matching the whole release
            // title can attach an unrelated release - e.g. a different entry in a franchise that
            // happens to carry the same English alternate name - to a library series. Global
            // resolution therefore trusts the parser's own series-name guess only.
            if (match == null &&
                restrictTo != null &&
                parsedEpisodeInfo.ReleaseTitle.IsNotNullOrWhiteSpace() &&
                parsedEpisodeInfo.ReleaseTitle != parsedEpisodeInfo.SeriesTitle)
            {
                match = _seriesTitleMatcher.Match(parsedEpisodeInfo.ReleaseTitle, restrictTo);
            }

            if (match != null)
            {
                _logger.Debug("Found matching series by fuzzy title match for: {0}", parsedEpisodeInfo.SeriesTitle);
            }

            return match;
        }

        private Episode GetDailyEpisode(Series series, string airDate, int? part, SearchCriteriaBase searchCriteria)
        {
            Episode episodeInfo = null;

            if (searchCriteria != null)
            {
                episodeInfo = searchCriteria.Episodes.SingleOrDefault(
                    e => e.AirDate == airDate);
            }

            if (episodeInfo == null)
            {
                episodeInfo = _episodeService.FindEpisode(series.Id, airDate, part);
            }

            return episodeInfo;
        }

        private List<Episode> GetAnimeEpisodes(Series series, ParsedEpisodeInfo parsedEpisodeInfo, int seasonNumber, bool sceneSource, SearchCriteriaBase searchCriteria)
        {
            var result = new List<Episode>();

            var sceneSeasonNumber = _sceneMappingService.GetSceneSeasonNumber(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle);

            foreach (var absoluteEpisodeNumber in parsedEpisodeInfo.AbsoluteEpisodeNumbers)
            {
                var episodes = new List<Episode>();

                if (parsedEpisodeInfo.Special)
                {
                    var episode = _episodeService.FindEpisode(series.Id, 0, absoluteEpisodeNumber);
                    episodes.AddIfNotNull(episode);
                }
                else if (sceneSource)
                {
                    // Is there a reason why we excluded season 1 from this handling before?
                    // Might have something to do with the scene name to season number check
                    // If this needs to be reverted tests will need to be added
                    if (sceneSeasonNumber.HasValue)
                    {
                        episodes = _episodeService.FindEpisodesBySceneNumbering(series.Id, sceneSeasonNumber.Value, absoluteEpisodeNumber);

                        if (episodes.Empty())
                        {
                            var episode = _episodeService.FindEpisode(series.Id, sceneSeasonNumber.Value, absoluteEpisodeNumber);
                            episodes.AddIfNotNull(episode);
                        }
                    }
                    else if (parsedEpisodeInfo.SeasonNumber > 1 && parsedEpisodeInfo.EpisodeNumbers.Empty())
                    {
                        episodes = _episodeService.FindEpisodesBySceneNumbering(series.Id, parsedEpisodeInfo.SeasonNumber.Value, absoluteEpisodeNumber);

                        if (episodes.Empty())
                        {
                            var episode = _episodeService.FindEpisode(series.Id, parsedEpisodeInfo.SeasonNumber.Value, absoluteEpisodeNumber);
                            episodes.AddIfNotNull(episode);
                        }
                    }
                    else
                    {
                        episodes = _episodeService.FindEpisodesBySceneNumbering(series.Id, absoluteEpisodeNumber);

                        // Don't allow multiple results without a scene name mapping.
                        if (episodes.Count > 1)
                        {
                            episodes.Clear();
                        }
                    }
                }

                if (episodes.Empty())
                {
                    // Absolute episode numbers can be duplicated across seasons (for example
                    // when a series also has specials). Without a scene mapping there is no
                    // unambiguous episode to pick, so mirror the scene-numbering handling above
                    // and leave the release unmapped instead of throwing.
                    var absoluteEpisodes = _episodeService.FindEpisodesByAbsoluteEpisodeNumber(series.Id, absoluteEpisodeNumber);

                    if (absoluteEpisodes != null && absoluteEpisodes.Count == 1)
                    {
                        episodes.Add(absoluteEpisodes[0]);
                    }
                }

                foreach (var episode in episodes)
                {
                    _logger.Debug("Using absolute episode number {0} for: {1} - TVDB: {2}x{3:00}",
                                absoluteEpisodeNumber,
                                series.Title,
                                episode.SeasonNumber,
                                episode.EpisodeNumber);

                    result.Add(episode);
                }
            }

            return result;
        }

        private List<Episode> GetStandardEpisodes(Series series, ParsedEpisodeInfo parsedEpisodeInfo, int mappedSeasonNumber, bool sceneSource, SearchCriteriaBase searchCriteria)
        {
            var result = new List<Episode>();

            if (parsedEpisodeInfo.EpisodeNumbers == null)
            {
                return new List<Episode>();
            }

            foreach (var episodeNumber in parsedEpisodeInfo.EpisodeNumbers)
            {
                if (series.UseSceneNumbering && sceneSource)
                {
                    var episodes = new List<Episode>();

                    if (searchCriteria != null)
                    {
                        episodes = searchCriteria.Episodes.Where(e => e.SceneSeasonNumber == parsedEpisodeInfo.SeasonNumber &&
                                                                      e.SceneEpisodeNumber == episodeNumber).ToList();
                    }

                    if (!episodes.Any())
                    {
                        episodes = _episodeService.FindEpisodesBySceneNumbering(series.Id, mappedSeasonNumber, episodeNumber);
                    }

                    if (episodes != null && episodes.Any())
                    {
                        _logger.Debug("Using Scene to TVDB Mapping for: {0} - Scene: {1}x{2:00} - TVDB: {3}",
                                    series.Title,
                                    episodes.First().SceneSeasonNumber,
                                    episodes.First().SceneEpisodeNumber,
                                    string.Join(", ", episodes.Select(e => string.Format("{0}x{1:00}", e.SeasonNumber, e.EpisodeNumber))));

                        result.AddRange(episodes);
                        continue;
                    }
                }

                Episode episodeInfo = null;

                if (searchCriteria != null)
                {
                    episodeInfo = searchCriteria.Episodes.SingleOrDefault(e => e.SeasonNumber == mappedSeasonNumber && e.EpisodeNumber == episodeNumber);
                }

                if (episodeInfo == null)
                {
                    episodeInfo = _episodeService.FindEpisode(series.Id, mappedSeasonNumber, episodeNumber);
                }

                if (episodeInfo != null)
                {
                    result.Add(episodeInfo);
                }
                else
                {
                    _logger.Debug("Unable to find {0}", parsedEpisodeInfo);
                }
            }

            return result;
        }

        public ParsedMovieInfo ParseMinimalPathMovieInfo(string path)
        {
            var fileInfo = new FileInfo(path);

            var result = Parser.ParseMovieTitle(fileInfo.Name, true);

            if (result == null)
            {
                _logger.Debug("Attempting to parse movie info using directory and file names. '{0}'", fileInfo.Directory.Name);
                result = Parser.ParseMovieTitle(fileInfo.Directory.Name + " " + fileInfo.Name);
            }

            if (result == null)
            {
                _logger.Debug("Attempting to parse movie info using directory name. '{0}'", fileInfo.Directory.Name);
                result = Parser.ParseMovieTitle(fileInfo.Directory.Name + fileInfo.Extension);
            }

            return result;
        }

        public Movie GetMovie(string title)
        {
            var parsedMovieInfo = Parser.ParseMovieTitle(title);

            if (parsedMovieInfo == null)
            {
                return _movieService.FindByTitle(title);
            }

            var result = TryGetMovieByTitleAndOrYear(parsedMovieInfo);

            if (result != null)
            {
                return result.Movie;
            }

            return null;
        }

        public RemoteMovie Map(ParsedMovieInfo parsedMovieInfo, string imdbId, int tmdbId, SearchCriteriaBase searchCriteria = null)
        {
            return Map(parsedMovieInfo, imdbId, tmdbId, null, searchCriteria);
        }

        public RemoteMovie Map(ParsedMovieInfo parsedMovieInfo, int movieId)
        {
            return new RemoteMovie
            {
                ParsedMovieInfo = parsedMovieInfo,
                Movie = _movieService.GetMovie(movieId)
            };
        }

        public RemoteMovie Map(ParsedMovieInfo parsedMovieInfo, string imdbId, int tmdbId, Movie movie, SearchCriteriaBase searchCriteria)
        {
            var remoteMovie = new RemoteMovie
            {
                ParsedMovieInfo = parsedMovieInfo
            };

            if (movie == null)
            {
                var movieMatch = FindMovie(parsedMovieInfo, imdbId, tmdbId, searchCriteria);

                if (movieMatch != null)
                {
                    movie = movieMatch.Movie;
                    remoteMovie.MovieMatchType = movieMatch.MatchType;
                }
            }

            if (movie != null)
            {
                remoteMovie.Movie = movie;
            }

            remoteMovie.Languages = parsedMovieInfo.Languages;

            if (searchCriteria != null)
            {
                remoteMovie.MovieRequested = remoteMovie.Movie?.Id == searchCriteria.Movie?.Id;
            }

            return remoteMovie;
        }

        private FindMovieResult FindMovie(ParsedMovieInfo parsedMovieInfo, string imdbId, int tmdbId, SearchCriteriaBase searchCriteria)
        {
            FindMovieResult result = null;

            if (!string.IsNullOrWhiteSpace(imdbId) && imdbId != "0")
            {
                result = TryGetMovieByImDbId(parsedMovieInfo, imdbId);
            }

            if (result == null && tmdbId > 0)
            {
                result = TryGetMovieByTmdbId(parsedMovieInfo, tmdbId);
            }

            if (result == null)
            {
                if (searchCriteria != null)
                {
                    result = TryGetMovieBySearchCriteria(parsedMovieInfo, imdbId, tmdbId, searchCriteria);
                }
                else
                {
                    result = TryGetMovieByTitleAndOrYear(parsedMovieInfo);
                }
            }

            if (result == null)
            {
                _logger.Debug($"No matching movie for titles '{string.Join(", ", parsedMovieInfo.MovieTitles)} ({parsedMovieInfo.Year})'");
            }

            return result;
        }

        private FindMovieResult TryGetMovieByImDbId(ParsedMovieInfo parsedMovieInfo, string imdbId)
        {
            var movie = _movieService.FindByImdbId(imdbId);

            // Should fix practically all problems, where indexer is shite at adding correct imdbids to movies.
            if (movie != null && (parsedMovieInfo.Year < 1800 || movie.MovieMetadata.Value.Year == parsedMovieInfo.Year || movie.MovieMetadata.Value.SecondaryYear == parsedMovieInfo.Year))
            {
                return new FindMovieResult(movie, MovieMatchType.Id);
            }

            return null;
        }

        private FindMovieResult TryGetMovieByTmdbId(ParsedMovieInfo parsedMovieInfo, int tmdbId)
        {
            var movie = _movieService.FindByTmdbId(tmdbId);

            // Should fix practically all problems, where indexer is shite at adding correct imdbids to movies.
            if (movie != null && (parsedMovieInfo.Year < 1800 || movie.MovieMetadata.Value.Year == parsedMovieInfo.Year || movie.MovieMetadata.Value.SecondaryYear == parsedMovieInfo.Year))
            {
                return new FindMovieResult(movie, MovieMatchType.Id);
            }

            return null;
        }

        private FindMovieResult TryGetMovieByTitleAndOrYear(ParsedMovieInfo parsedMovieInfo)
        {
            var candidates = _movieService.FindByTitleCandidates(parsedMovieInfo.MovieTitles, out var otherTitles);

            Movie movieByTitleAndOrYear;
            if (parsedMovieInfo.Year > 1800)
            {
                movieByTitleAndOrYear = _movieService.FindByTitle(parsedMovieInfo.MovieTitles, parsedMovieInfo.Year, otherTitles, candidates);
                if (movieByTitleAndOrYear != null)
                {
                    return new FindMovieResult(movieByTitleAndOrYear, MovieMatchType.Title);
                }

                return null;
            }

            movieByTitleAndOrYear = _movieService.FindByTitle(parsedMovieInfo.MovieTitles, null, otherTitles, candidates);
            if (movieByTitleAndOrYear != null)
            {
                return new FindMovieResult(movieByTitleAndOrYear, MovieMatchType.Title);
            }

            return null;
        }

        private FindMovieResult TryGetMovieBySearchCriteria(ParsedMovieInfo parsedMovieInfo, string imdbId, int tmdbId, SearchCriteriaBase searchCriteria)
        {
            Movie possibleMovie = null;

            var possibleTitles = new List<string>
            {
                searchCriteria.Movie.MovieMetadata.Value.CleanTitle
            };
            possibleTitles.AddIfNotNull(searchCriteria.Movie.MovieMetadata.Value.CleanOriginalTitle);
            possibleTitles.AddRange(searchCriteria.Movie.MovieMetadata.Value.AlternativeTitles.Select(t => t.CleanTitle));
            possibleTitles.AddRange(searchCriteria.Movie.MovieMetadata.Value.Translations.Select(t => t.CleanTitle));

            var cleanTitles = parsedMovieInfo.MovieTitles.Select(t => t.CleanMovieTitle()).ToArray();

            if (possibleTitles.Any(pt =>
                cleanTitles.Contains(pt)
                || _arabicRomanNumeralMappings.Any(mn =>
                    cleanTitles.Contains(pt.Replace(mn.ArabicNumeralAsString, mn.RomanNumeralLowerCase))
                    || cleanTitles.Any(t => t.Replace(mn.ArabicNumeralAsString, mn.RomanNumeralLowerCase) == pt))))
            {
                possibleMovie = searchCriteria.Movie;
            }

            if (possibleMovie != null)
            {
                if (parsedMovieInfo.Year < 1800 || possibleMovie.MovieMetadata.Value.Year == parsedMovieInfo.Year || possibleMovie.MovieMetadata.Value.SecondaryYear == parsedMovieInfo.Year)
                {
                    return new FindMovieResult(possibleMovie, MovieMatchType.Title);
                }
            }

            if (tmdbId > 0 && tmdbId == searchCriteria.Movie.TmdbId)
            {
                return new FindMovieResult(searchCriteria.Movie, MovieMatchType.Id);
            }

            if (imdbId.IsNotNullOrWhiteSpace() && imdbId == searchCriteria.Movie.ImdbId)
            {
                return new FindMovieResult(searchCriteria.Movie, MovieMatchType.Id);
            }

            return null;
        }
    }
}
