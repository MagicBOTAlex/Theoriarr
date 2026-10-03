using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using Sonarr.Api.V3.Indexers;
using Sonarr.Http;
using Sonarr.Http.Subsystem;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Sonarr.Api.V3.Movies.Indexers
{
    [V3ApiController("release")]
    [AppSubsystem(AppSubsystem.Movies)]
    public class ReleaseController : Controller
    {
        private readonly IFetchAndParseRss _rssFetcherAndParser;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IDownloadService _downloadService;
        private readonly IMovieService _movieService;
        private readonly IHistoryService _historyService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly IReleaseSearchCache _releaseSearchCache;
        private readonly Logger _logger;

        private readonly ICached<RemoteMovie> _remoteMovieCache;

        public ReleaseController(IFetchAndParseRss rssFetcherAndParser,
                             ISearchForReleases releaseSearchService,
                             IMakeDownloadDecision downloadDecisionMaker,
                             IPrioritizeDownloadDecision prioritizeDownloadDecision,
                             IDownloadService downloadService,
                             IMovieService movieService,
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
            _movieService = movieService;
            _historyService = historyService;
            _qualityProfileService = qualityProfileService;
            _releaseSearchCache = releaseSearchCache;
            _logger = logger;

            _remoteMovieCache = cacheManager.GetCache<RemoteMovie>(GetType(), "remoteMovies");
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<object> DownloadRelease([FromBody] ReleaseResource release)
        {
            var remoteMovie = _remoteMovieCache.Find(GetCacheKey(release));

            if (remoteMovie == null)
            {
                _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            try
            {
                if (release.ShouldOverride == true)
                {
                    Ensure.That(release.MovieId, () => release.MovieId).IsNotNull();
                    Ensure.That(release.Quality, () => release.Quality).IsNotNull();
                    Ensure.That(release.Languages, () => release.Languages).IsNotNull();

                    remoteMovie = new RemoteMovie
                    {
                        Release = remoteMovie.Release,
                        ParsedMovieInfo = remoteMovie.ParsedMovieInfo.JsonClone(),
                        MovieRequested = remoteMovie.MovieRequested,
                        DownloadAllowed = remoteMovie.DownloadAllowed,
                        SeedConfiguration = remoteMovie.SeedConfiguration,
                        CustomFormats = remoteMovie.CustomFormats,
                        CustomFormatScore = remoteMovie.CustomFormatScore,
                        MovieMatchType = remoteMovie.MovieMatchType,
                        ReleaseSource = remoteMovie.ReleaseSource
                    };

                    remoteMovie.Movie = _movieService.GetMovie(release.MovieId!.Value);
                    remoteMovie.ParsedMovieInfo.Quality = release.Quality;
                    remoteMovie.Languages = release.Languages;
                }

                if (remoteMovie.Movie == null)
                {
                    if (release.MovieId.HasValue)
                    {
                        remoteMovie.Movie = _movieService.GetMovie(release.MovieId.Value);
                    }
                    else
                    {
                        throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to find matching movie, will need to be manually provided");
                    }
                }

                await _downloadService.DownloadReport(remoteMovie, release.DownloadClientId);
            }
            catch (ReleaseDownloadException ex)
            {
                _logger.Error(ex, ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.Conflict, "Getting release from indexer failed");
            }

            return release;
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<List<ReleaseResource>> GetReleases(int? movieId, string searchId = null, bool useCached = false)
        {
            using (ReleaseSearchProgressContext.Begin(searchId))
            {
                if (movieId.HasValue)
                {
                    return await GetMovieReleases(movieId.Value, searchId, useCached);
                }

                return await GetRss();
            }
        }

        // Lets the client ask whether a recent interactive search for this movie can be reused
        // before it decides to search the indexers again.
        [HttpGet("cached")]
        [Produces("application/json")]
        public object GetCachedSearch(int? movieId)
        {
            var cached = movieId.HasValue ? _releaseSearchCache.Get($"movie:{movieId.Value}") : null;

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
        private Task<List<DownloadDecision>> Search(string searchId, string cacheKey, ReleaseSearchDomain domain, Func<Task<List<DownloadDecision>>> search)
        {
            if (searchId.IsNotNullOrWhiteSpace())
            {
                return Task.FromResult(_releaseSearchService.StartInteractiveSearch(searchId, cacheKey, domain, search));
            }

            return search();
        }

        private async Task<List<ReleaseResource>> GetMovieReleases(int movieId, string searchId, bool useCached)
        {
            try
            {
                var cacheKey = $"movie:{movieId}";
                List<DownloadDecision> decisions;

                if (useCached)
                {
                    decisions = _releaseSearchCache.Get(cacheKey)?.Decisions
                                ?? await Search(searchId, cacheKey, ReleaseSearchDomain.Movies, () => _releaseSearchService.MovieSearch(movieId, true, true));
                }
                else
                {
                    decisions = await Search(searchId, cacheKey, ReleaseSearchDomain.Movies, () => _releaseSearchService.MovieSearch(movieId, true, true));
                }

                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisionsForMovies(decisions);
                var history = _historyService.FindByMovieId(movieId);

                return MapDecisions(prioritizedDecisions, history);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Movie search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetRss()
        {
            var reports = await _rssFetcherAndParser.Fetch();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports)
                                                  .Where(d => d.RemoteMovie != null)
                                                  .ToList();
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisionsForMovies(decisions);

            return MapDecisions(prioritizedDecisions, new List<MovieHistory>());
        }

        private ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var release = decision.ToResource();
            var qualityProfile = _qualityProfileService.GetDefaultProfile(string.Empty);

            release.ReleaseWeight = initialWeight;
            release.QualityWeight = qualityProfile.GetIndex(release.Quality.Quality).Index * 100;
            release.QualityWeight += release.Quality.Revision.Real * 10;
            release.QualityWeight += release.Quality.Revision.Version;

            _remoteMovieCache.Set(GetCacheKey(release), decision.RemoteMovie, TimeSpan.FromMinutes(30));

            return release;
        }

        private string GetCacheKey(ReleaseResource resource)
        {
            return string.Concat(resource.IndexerId, "_", resource.Guid);
        }

        private List<ReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions, List<MovieHistory> history)
        {
            var result = new List<ReleaseResource>();

            foreach (var downloadDecision in decisions)
            {
                var release = MapDecision(downloadDecision, result.Count);

                release.History = AddHistory(downloadDecision.RemoteMovie.Release, history);

                result.Add(release);
            }

            return result;
        }

        private ReleaseHistoryResource AddHistory(ReleaseInfo release, List<MovieHistory> history)
        {
            if (history == null || !history.Any())
            {
                return null;
            }

            var grabbed = history.FirstOrDefault(h => h.EventType == MovieHistoryEventType.Grabbed &&
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
                    grabbed = history.FirstOrDefault(h => h.EventType == MovieHistoryEventType.Grabbed &&
                                                          ReleaseComparer.SameTorrent(new ReleaseComparerModel(h), torrentInfo));
                }
            }
            else if (grabbed == null)
            {
                grabbed = history.FirstOrDefault(h => h.EventType == MovieHistoryEventType.Grabbed &&
                                                      ReleaseComparer.SameNzb(new ReleaseComparerModel(h), release));
            }

            if (grabbed != null)
            {
                var resource = new ReleaseHistoryResource
                {
                    Grabbed = grabbed.Date,
                };

                var failedHistory = history.FirstOrDefault(h => h.EventType == MovieHistoryEventType.DownloadFailed &&
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
}
