using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Sonarr.Api.V5.Release;

[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
public class ReleaseController : RestController<ReleaseResource>
{
    private readonly IFetchAndParseRss _rssFetcherAndParser;
    private readonly ISearchForReleases _releaseSearchService;
    private readonly IMakeDownloadDecision _downloadDecisionMaker;
    private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
    private readonly IDownloadService _downloadService;
    private readonly ISeriesService _seriesService;
    private readonly IEpisodeService _episodeService;
    private readonly IParsingService _parsingService;
    private readonly IHistoryService _historyService;
    private readonly IReleaseSearchCache _releaseSearchCache;
    private readonly Logger _logger;

    private readonly QualityProfile _qualityProfile;
    private readonly ICached<RemoteEpisode> _remoteEpisodeCache;

    public ReleaseController(IFetchAndParseRss rssFetcherAndParser,
                         ISearchForReleases releaseSearchService,
                         IMakeDownloadDecision downloadDecisionMaker,
                         IPrioritizeDownloadDecision prioritizeDownloadDecision,
                         IDownloadService downloadService,
                         ISeriesService seriesService,
                         IEpisodeService episodeService,
                         IParsingService parsingService,
                         IHistoryService historyService,
                         IReleaseSearchCache releaseSearchCache,
                         ICacheManager cacheManager,
                         IQualityProfileService qualityProfileService,
                         Logger logger)
    {
        _rssFetcherAndParser = rssFetcherAndParser;
        _releaseSearchService = releaseSearchService;
        _downloadDecisionMaker = downloadDecisionMaker;
        _prioritizeDownloadDecision = prioritizeDownloadDecision;
        _downloadService = downloadService;
        _seriesService = seriesService;
        _episodeService = episodeService;
        _parsingService = parsingService;
        _historyService = historyService;
        _releaseSearchCache = releaseSearchCache;
        _logger = logger;

        _qualityProfile = qualityProfileService.GetDefaultProfile(string.Empty);
        _remoteEpisodeCache = cacheManager.GetCache<RemoteEpisode>(GetType(), "remoteEpisodes");

        PostValidator.RuleFor(s => s.Release).NotNull();
        PostValidator.RuleFor(s => s.Release!.IndexerId).ValidId();
        PostValidator.RuleFor(s => s.Release!.Guid).NotEmpty();
    }

    [NonAction]
    public override Results<Ok<ReleaseResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
    {
        return base.GetResourceByIdWithErrorHandler(id);
    }

    protected override ReleaseResource GetResourceById(int id)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<Results<Ok<ReleaseGrabResource>, NotFound>> DownloadRelease([FromBody] ReleaseGrabResource release)
    {
        var remoteEpisode = _remoteEpisodeCache.Find(GetCacheKey(release));

        if (remoteEpisode == null)
        {
            _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

            throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
        }

        try
        {
            if (release.Override != null)
            {
                var overrideInfo = release.Override;

                Ensure.That(overrideInfo.SeriesId, () => release.Override.SeriesId).IsNotNull();
                Ensure.That(overrideInfo.EpisodeIds, () => overrideInfo.EpisodeIds).IsNotNull();
                Ensure.That(overrideInfo.EpisodeIds, () => overrideInfo.EpisodeIds).HasItems();
                Ensure.That(overrideInfo.Quality, () => overrideInfo.Quality).IsNotNull();
                Ensure.That(overrideInfo.Languages, () => overrideInfo.Languages).IsNotNull();

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

                remoteEpisode.Series = _seriesService.GetSeries(overrideInfo.SeriesId!.Value);
                remoteEpisode.Episodes = _episodeService.GetEpisodes(overrideInfo.EpisodeIds);
                remoteEpisode.ParsedEpisodeInfo.Quality = overrideInfo.Quality;
                remoteEpisode.Languages = overrideInfo.Languages;
            }

            if (remoteEpisode.Series == null)
            {
                if (release.SearchInfo?.EpisodeId.HasValue == true)
                {
                    var episode = _episodeService.GetEpisode(release.SearchInfo.EpisodeId.Value);

                    remoteEpisode.Series = _seriesService.GetSeries(episode.SeriesId);
                    remoteEpisode.Episodes = new List<Episode> { episode };
                }
                else if (release.SearchInfo?.SeriesId.HasValue == true)
                {
                    var series = _seriesService.GetSeries(release.SearchInfo.SeriesId.Value);
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

                if (episodes.Empty() && release.SearchInfo?.EpisodeId.HasValue == true)
                {
                    var episode = _episodeService.GetEpisode(release.SearchInfo.EpisodeId.Value);

                    episodes = new List<Episode> { episode };
                }

                remoteEpisode.Episodes = episodes;
            }

            if (remoteEpisode.Episodes.Empty())
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse episodes in the release, will need to be manually provided");
            }

            await _downloadService.DownloadReport(remoteEpisode, release.Override?.DownloadClientId);
        }
        catch (ReleaseDownloadException ex)
        {
            _logger.Error(ex, ex.Message);
            throw new NzbDroneClientException(HttpStatusCode.Conflict, $"Getting release from indexer failed: {ex.Message}");
        }

        return TypedResults.Ok(release);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<Results<Ok<List<ReleaseResource>>, BadRequest>> GetReleases(int? seriesId, int? episodeId, int? seasonNumber, string? searchId = null, bool useCached = false)
    {
        using (ReleaseSearchProgressContext.Begin(searchId))
        {
            if (episodeId.HasValue)
            {
                return TypedResults.Ok(await GetEpisodeReleases(episodeId.Value, searchId, useCached));
            }

            if (seriesId.HasValue && seasonNumber.HasValue)
            {
                return TypedResults.Ok(await GetSeasonReleases(seriesId.Value, seasonNumber.Value, searchId, useCached));
            }

            return TypedResults.Ok(await GetRss());
        }
    }

    // Lets the client ask whether a recent interactive search for this target can be reused
    // before it decides to search the indexers again.
    [HttpGet("cached")]
    [Produces("application/json")]
    public object GetCachedSearch(int? seriesId, int? episodeId, int? seasonNumber)
    {
        var cacheKey = GetSearchCacheKey(seriesId, episodeId, seasonNumber);
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

    // Aborts a background search when the client navigates away or closes the window.
    [HttpDelete]
    [Produces("application/json")]
    public IActionResult CancelSearch(string searchId)
    {
        if (searchId.IsNotNullOrWhiteSpace())
        {
            _releaseSearchService.CancelInteractiveSearch(searchId);
        }

        return Ok();
    }

    // Interactive searches run in the background so the client gets partial results and can
    // pull newly finished indexers without waiting for the slowest one.
    private Task<List<DownloadDecision>> Search(string? searchId, string cacheKey, ReleaseSearchDomain domain, Func<Task<List<DownloadDecision>>> search)
    {
        if (searchId.IsNotNullOrWhiteSpace())
        {
            return Task.FromResult(_releaseSearchService.StartInteractiveSearch(searchId!, cacheKey, domain, search));
        }

        return search();
    }

    // Returns the cached decisions for the target when the client asked to reuse them, otherwise
    // starts a new interactive search (which also refreshes the cache on completion).
    private Task<List<DownloadDecision>> GetDecisions(string cacheKey, string? searchId, ReleaseSearchDomain domain, bool useCached, Func<Task<List<DownloadDecision>>> search)
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

    private static string? GetSearchCacheKey(int? seriesId, int? episodeId, int? seasonNumber)
    {
        if (episodeId.HasValue)
        {
            return $"episode:{episodeId.Value}";
        }

        if (seriesId.HasValue && seasonNumber.HasValue)
        {
            return $"series:{seriesId.Value}:season:{seasonNumber.Value}";
        }

        return null;
    }

    private async Task<List<ReleaseResource>> GetEpisodeReleases(int episodeId, string? searchId, bool useCached)
    {
        try
        {
            var decisions = await GetDecisions($"episode:{episodeId}", searchId, ReleaseSearchDomain.Series, useCached, () => _releaseSearchService.EpisodeSearch(episodeId, true, true));
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);
            var history = _historyService.FindByEpisodeId(episodeId);

            return MapDecisions(prioritizedDecisions, history);
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

    private async Task<List<ReleaseResource>> GetSeasonReleases(int seriesId, int seasonNumber, string? searchId, bool useCached)
    {
        try
        {
            var decisions = await GetDecisions($"series:{seriesId}:season:{seasonNumber}", searchId, ReleaseSearchDomain.Series, useCached, () => _releaseSearchService.SeasonSearch(seriesId, seasonNumber, false, false, true, true));
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);
            var history = _historyService.GetBySeason(seriesId, seasonNumber, null);

            return MapDecisions(prioritizedDecisions, history);
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

        return MapDecisions(prioritizedDecisions, new List<EpisodeHistory>());
    }

    private string GetCacheKey(ReleaseResource resource)
    {
        return string.Concat(resource.Release!.IndexerId, "_", resource.Release!.Guid);
    }

    private string GetCacheKey(ReleaseGrabResource resource)
    {
        return string.Concat(resource.IndexerId, "_", resource.Guid);
    }

    private List<ReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions, List<EpisodeHistory> history)
    {
        var result = new List<ReleaseResource>();

        foreach (var downloadDecision in decisions)
        {
            var release = downloadDecision.MapDecision(result.Count, _qualityProfile);

            release.History = AddHistory(downloadDecision.RemoteEpisode.Release, history);
            _remoteEpisodeCache.Set(GetCacheKey(release), downloadDecision.RemoteEpisode, TimeSpan.FromMinutes(30));

            result.Add(release);
        }

        return result;
    }

    private ReleaseHistoryResource? AddHistory(ReleaseInfo release, List<EpisodeHistory> history)
    {
        var grabbed = history.FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed &&
                                                  ((h.Data.TryGetValue("guid", out var guid) && guid == release.Guid) ||
                                                   (h.Data.TryGetValue("nzbInfoUrl", out var infoUrl) && infoUrl.IsNotNullOrWhiteSpace() && infoUrl.Equals(release.InfoUrl, StringComparison.Ordinal))));

        if (grabbed == null && release.DownloadProtocol == DownloadProtocol.Torrent)
        {
            if (release is not TorrentInfo torrentInfo)
            {
                return null;
            }

            if (torrentInfo.InfoHash.IsNotNullOrWhiteSpace())
            {
                grabbed = history.FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed &&
                                                      ReleaseComparer.SameTorrent(new ReleaseComparerModel(h),
                                                          torrentInfo));
            }

            if (grabbed == null)
            {
                grabbed = history.FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed &&
                                                      h.SourceTitle == release.Title &&
                                                      (DownloadProtocol)Convert.ToInt32(
                                                          h.Data.GetValueOrDefault("protocol")) ==
                                                      DownloadProtocol.Torrent &&
                                                      ReleaseComparer.SameTorrent(new ReleaseComparerModel(h),
                                                          torrentInfo));
            }
        }
        else if (grabbed == null)
        {
            grabbed = history.FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed &&
                                                  ReleaseComparer.SameNzb(new ReleaseComparerModel(h),
                                                      release));
        }

        if (grabbed != null)
        {
            var resource = new ReleaseHistoryResource
            {
                Grabbed = grabbed.Date,
            };

            var failedHistory = history.FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.DownloadFailed &&
                                                            h.DownloadId == grabbed.DownloadId);

            if (failedHistory != null)
            {
                resource.Failed = failedHistory.Date;
            }

            return resource;
        }

        return null;
    }
}
