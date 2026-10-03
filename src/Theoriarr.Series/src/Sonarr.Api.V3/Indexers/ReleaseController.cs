using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;
using Sonarr.Http;
using Sonarr.Http.Subsystem;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Sonarr.Api.V3.Indexers
{
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    public class ReleaseController : ReleaseControllerBase
    {
        private readonly IFetchAndParseRss _rssFetcherAndParser;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IDownloadService _downloadService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IParsingService _parsingService;
        private readonly IReleaseSearchCache _releaseSearchCache;
        private readonly Logger _logger;

        private readonly ICached<RemoteEpisode> _remoteEpisodeCache;

        public ReleaseController(IFetchAndParseRss rssFetcherAndParser,
                             ISearchForReleases releaseSearchService,
                             IMakeDownloadDecision downloadDecisionMaker,
                             IPrioritizeDownloadDecision prioritizeDownloadDecision,
                             IDownloadService downloadService,
                             ISeriesService seriesService,
                             IEpisodeService episodeService,
                             IParsingService parsingService,
                             IReleaseSearchCache releaseSearchCache,
                             ICacheManager cacheManager,
                             IQualityProfileService qualityProfileService,
                             Logger logger)
            : base(qualityProfileService)
        {
            _rssFetcherAndParser = rssFetcherAndParser;
            _releaseSearchService = releaseSearchService;
            _downloadDecisionMaker = downloadDecisionMaker;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _downloadService = downloadService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _parsingService = parsingService;
            _releaseSearchCache = releaseSearchCache;
            _logger = logger;

            PostValidator.RuleFor(s => s.IndexerId).ValidId();
            PostValidator.RuleFor(s => s.Guid).NotEmpty();

            _remoteEpisodeCache = cacheManager.GetCache<RemoteEpisode>(GetType(), "remoteEpisodes");
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<object> DownloadRelease([FromBody] ReleaseResource release)
        {
            var remoteEpisode = _remoteEpisodeCache.Find(GetCacheKey(release));

            if (remoteEpisode == null)
            {
                _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            try
            {
                if (release.ShouldOverride == true)
                {
                    Ensure.That(release.SeriesId, () => release.SeriesId).IsNotNull();
                    Ensure.That(release.EpisodeIds, () => release.EpisodeIds).IsNotNull();
                    Ensure.That(release.EpisodeIds, () => release.EpisodeIds).HasItems();
                    Ensure.That(release.Quality, () => release.Quality).IsNotNull();
                    Ensure.That(release.Languages, () => release.Languages).IsNotNull();

                    // Clone the remote episode so we don't overwrite anything on the original
                    remoteEpisode = new RemoteEpisode
                    {
                        Release = remoteEpisode.Release,
                        ParsedEpisodeInfo = remoteEpisode.ParsedEpisodeInfo.JsonClone(),
                        SceneMapping = remoteEpisode.SceneMapping,
                        MappedSeasonNumber = remoteEpisode.MappedSeasonNumber,
                        EpisodeRequested = remoteEpisode.EpisodeRequested,
                        DownloadAllowed = remoteEpisode.DownloadAllowed,
                        SeedConfiguration = remoteEpisode.SeedConfiguration,
                        CustomFormats = remoteEpisode.CustomFormats,
                        CustomFormatScore = remoteEpisode.CustomFormatScore,
                        SeriesMatchType = remoteEpisode.SeriesMatchType,
                        ReleaseSource = remoteEpisode.ReleaseSource
                    };

                    remoteEpisode.Series = _seriesService.GetSeries(release.SeriesId!.Value);
                    remoteEpisode.Episodes = _episodeService.GetEpisodes(release.EpisodeIds);
                    remoteEpisode.ParsedEpisodeInfo.Quality = release.Quality;
                    remoteEpisode.Languages = release.Languages;
                }

                if (remoteEpisode.Series == null)
                {
                    if (release.EpisodeId.HasValue)
                    {
                        var episode = _episodeService.GetEpisode(release.EpisodeId.Value);

                        remoteEpisode.Series = _seriesService.GetSeries(episode.SeriesId);
                        remoteEpisode.Episodes = new List<Episode> { episode };
                    }
                    else if (release.SeriesId.HasValue)
                    {
                        var series = _seriesService.GetSeries(release.SeriesId.Value);
                        var episodes = _parsingService.GetEpisodes(remoteEpisode.ParsedEpisodeInfo, series, true);

                        if (episodes.Empty())
                        {
                            throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse episodes in the release, will need to be manually provided");
                        }

                        remoteEpisode.Series = series;
                        remoteEpisode.Episodes = episodes;
                    }
                    else
                    {
                        throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to find matching series and episodes, will need to be manually provided");
                    }
                }
                else if (remoteEpisode.Episodes.Empty())
                {
                    var episodes = _parsingService.GetEpisodes(remoteEpisode.ParsedEpisodeInfo, remoteEpisode.Series, true);

                    if (episodes.Empty() && release.EpisodeId.HasValue)
                    {
                        var episode = _episodeService.GetEpisode(release.EpisodeId.Value);

                        episodes = new List<Episode> { episode };
                    }

                    remoteEpisode.Episodes = episodes;
                }

                if (remoteEpisode.Episodes.Empty())
                {
                    throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse episodes in the release, will need to be manually provided");
                }

                await _downloadService.DownloadReport(remoteEpisode, release.DownloadClientId);
            }
            catch (ReleaseDownloadException ex)
            {
                _logger.Error(ex, ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.Conflict, $"Getting release from indexer failed: {ex.Message}");
            }

            return release;
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<List<ReleaseResource>> GetReleases(int? seriesId, int? episodeId, int? seasonNumber, string searchId = null, bool useCached = false)
        {
            using (ReleaseSearchProgressContext.Begin(searchId))
            {
                if (episodeId.HasValue)
                {
                    return await GetEpisodeReleases(episodeId.Value, searchId, useCached);
                }

                if (seriesId.HasValue && seasonNumber.HasValue)
                {
                    return await GetSeasonReleases(seriesId.Value, seasonNumber.Value, searchId, useCached);
                }

                return await GetRss();
            }
        }

        // Lets the client ask whether a recent interactive search for this target can be reused
        // before it decides to search the indexers again.
        [HttpGet("cached")]
        [Produces("application/json")]
        public object GetCachedSearch(int? seriesId, int? episodeId, int? seasonNumber)
        {
            var cacheKey = episodeId.HasValue
                ? $"episode:{episodeId.Value}"
                : (seriesId.HasValue && seasonNumber.HasValue ? $"series:{seriesId.Value}:season:{seasonNumber.Value}" : null);
            var cached = cacheKey == null ? null : _releaseSearchCache.Get(cacheKey);

            if (cached == null)
            {
                return new { available = false };
            }

            return new
            {
                available = true,
                cachedAt = cached.CachedAt,
                count = cached.Decisions?.Count ?? 0
            };
        }

        // Interactive searches run in the background so the client gets partial results and can
        // pull newly finished indexers without waiting for the slowest one.
        private Task<List<DownloadDecision>> Search(string searchId, string cacheKey, ReleaseSearchDomain domain, Func<Task<List<DownloadDecision>>> search)
        {
            if (searchId.IsNotNullOrWhiteSpace())
            {
                return Task.FromResult(_releaseSearchService.StartInteractiveSearch(searchId, cacheKey, domain, search));
            }

            return search();
        }

        // Returns the cached decisions for the target when the client asked to reuse them, otherwise
        // starts a new interactive search (which also refreshes the cache on completion).
        private Task<List<DownloadDecision>> GetDecisions(string cacheKey, string searchId, ReleaseSearchDomain domain, bool useCached, Func<Task<List<DownloadDecision>>> search)
        {
            if (useCached)
            {
                var cached = _releaseSearchCache.Get(cacheKey);

                if (cached != null)
                {
                    return Task.FromResult(cached.Decisions ?? new List<DownloadDecision>());
                }
            }

            return Search(searchId, cacheKey, domain, search);
        }

        private async Task<List<ReleaseResource>> GetEpisodeReleases(int episodeId, string searchId, bool useCached)
        {
            try
            {
                var decisions = await GetDecisions($"episode:{episodeId}", searchId, ReleaseSearchDomain.Series, useCached, () => _releaseSearchService.EpisodeSearch(episodeId, true, true));
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Episode search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetSeasonReleases(int seriesId, int seasonNumber, string searchId, bool useCached)
        {
            try
            {
                var decisions = await GetDecisions($"series:{seriesId}:season:{seasonNumber}", searchId, ReleaseSearchDomain.Series, useCached, () => _releaseSearchService.SeasonSearch(seriesId, seasonNumber, false, false, true, true));
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Season search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetRss()
        {
            var reports = await _rssFetcherAndParser.Fetch();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

            return MapDecisions(prioritizedDecisions);
        }

        protected override ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var resource = base.MapDecision(decision, initialWeight);
            _remoteEpisodeCache.Set(GetCacheKey(resource), decision.RemoteEpisode, TimeSpan.FromMinutes(30));

            return resource;
        }

        private string GetCacheKey(ReleaseResource resource)
        {
            return string.Concat(resource.IndexerId, "_", resource.Guid);
        }
    }
}
