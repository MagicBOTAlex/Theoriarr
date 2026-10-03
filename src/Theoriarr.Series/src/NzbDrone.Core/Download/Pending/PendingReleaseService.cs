using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Download.Pending
{
    public interface IPendingReleaseService
    {
        void Add(DownloadDecision decision, PendingReleaseReason reason);
        void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions);
        List<ReleaseInfo> GetPending();
        List<RemoteEpisode> GetPendingRemoteEpisodes(int seriesId);
        List<RemoteMovie> GetPendingRemoteMovies(int movieId);
        List<Queue.Queue> GetPendingQueue();
        Queue.Queue FindPendingQueueItem(int queueId);
        void RemovePendingQueueItems(int queueId);
        RemoteEpisode OldestPendingRelease(int seriesId, int[] episodeIds);
        RemoteMovie OldestPendingRelease(int movieId);
        List<Queue.Queue> GetPendingQueueObsolete();
        Queue.Queue FindPendingQueueItemObsolete(int queueId);
        void RemovePendingQueueItemsObsolete(int queueId);
    }

    public class PendingReleaseService : IPendingReleaseService,
                                         IHandle<SeriesEditedEvent>,
                                         IHandle<SeriesUpdatedEvent>,
                                         IHandle<SeriesDeletedEvent>,
                                         IHandle<EpisodeGrabbedEvent>,
                                         IHandle<MovieGrabbedEvent>,
                                         IHandle<MoviesDeletedEvent>,
                                         IHandle<RssSyncCompleteEvent>,
                                         IHandle<QualityProfileUpdatedEvent>,
                                         IHandle<ConfigSavedEvent>,
                                         IHandle<ApplicationStartedEvent>
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IPendingReleaseRepository _repository;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly IParsingService _parsingService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly ITaskManager _taskManager;
        private readonly IConfigService _configService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly IRemoteMovieAggregationService _movieAggregationService;
        private readonly IDownloadClientFactory _downloadClientFactory;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private static List<PendingRelease> _pendingReleases = new();

        public PendingReleaseService(IIndexerStatusService indexerStatusService,
                                    IPendingReleaseRepository repository,
                                    ISeriesService seriesService,
                                    IMovieService movieService,
                                    IParsingService parsingService,
                                    IDelayProfileService delayProfileService,
                                    ITaskManager taskManager,
                                    IConfigService configService,
                                    ICustomFormatCalculationService formatCalculator,
                                    IRemoteEpisodeAggregationService aggregationService,
                                    IRemoteMovieAggregationService movieAggregationService,
                                    IDownloadClientFactory downloadClientFactory,
                                    IIndexerFactory indexerFactory,
                                    IEventAggregator eventAggregator,
                                    Logger logger)
        {
            _indexerStatusService = indexerStatusService;
            _repository = repository;
            _seriesService = seriesService;
            _movieService = movieService;
            _parsingService = parsingService;
            _delayProfileService = delayProfileService;
            _taskManager = taskManager;
            _configService = configService;
            _formatCalculator = formatCalculator;
            _aggregationService = aggregationService;
            _movieAggregationService = movieAggregationService;
            _downloadClientFactory = downloadClientFactory;
            _indexerFactory = indexerFactory;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Add(DownloadDecision decision, PendingReleaseReason reason)
        {
            AddMany(new List<Tuple<DownloadDecision, PendingReleaseReason>> { Tuple.Create(decision, reason) });
        }

        public void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions)
        {
            var episodeDecisions = decisions.Where(v => v.Item1.RemoteMovie == null && v.Item1.RemoteEpisode != null).ToList();
            var movieDecisions = decisions.Where(v => v.Item1.RemoteMovie != null).ToList();

            AddManyEpisodes(episodeDecisions);
            AddManyMovies(movieDecisions);

            UpdatePendingReleases();
        }

        private void AddManyEpisodes(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions)
        {
            foreach (var seriesDecisions in decisions.GroupBy(v => v.Item1.RemoteEpisode.Series.Id))
            {
                var series = seriesDecisions.First().Item1.RemoteEpisode.Series;
                var alreadyPending = _pendingReleases.Where(p => p.SeriesId == series.Id).SelectList(s => s.Clone());

                // TODO: Do we need IncludeRemoteEpisodes?
                alreadyPending = IncludeRemoteEpisodes(alreadyPending, seriesDecisions.ToDictionaryIgnoreDuplicates(v => v.Item1.RemoteEpisode.Release.Title, v => v.Item1.RemoteEpisode));
                var alreadyPendingByEpisode = CreateEpisodeLookup(alreadyPending);

                foreach (var pair in seriesDecisions)
                {
                    var decision = pair.Item1;
                    var reason = pair.Item2;

                    var episodeIds = decision.RemoteEpisode.Episodes.Select(e => e.Id);

                    var existingReports = episodeIds.SelectMany(v => alreadyPendingByEpisode[v])
                                                    .Distinct().ToList();

                    var matchingReports = existingReports.Where(MatchingReleasePredicate(decision.RemoteEpisode.Release)).ToList();

                    if (matchingReports.Any())
                    {
                        var matchingReport = matchingReports.First();

                        if (matchingReport.Reason != reason)
                        {
                            if (matchingReport.Reason == PendingReleaseReason.DownloadClientUnavailable)
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, not changing reason", decision.RemoteEpisode, matchingReport.Reason);
                            }
                            else
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, changing to {2}", decision.RemoteEpisode, matchingReport.Reason, reason);
                                matchingReport.Reason = reason;
                                _repository.Update(matchingReport);
                            }
                        }
                        else
                        {
                            _logger.Debug("The release {0} is already pending with reason {1}, not adding again", decision.RemoteEpisode, reason);
                        }

                        if (matchingReports.Count > 1)
                        {
                            _logger.Debug("The release {0} had {1} duplicate pending, removing duplicates.", decision.RemoteEpisode, matchingReports.Count - 1);

                            foreach (var duplicate in matchingReports.Skip(1))
                            {
                                _repository.Delete(duplicate.Id);
                                alreadyPending.Remove(duplicate);
                                alreadyPendingByEpisode = CreateEpisodeLookup(alreadyPending);
                            }
                        }

                        continue;
                    }

                    _logger.Debug("Adding release {0} to pending releases with reason {1}", decision.RemoteEpisode, reason);
                    Insert(decision, reason);
                }
            }
        }

        private void AddManyMovies(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions)
        {
            foreach (var movieDecisions in decisions.GroupBy(v => v.Item1.RemoteMovie.Movie.Id))
            {
                var movie = movieDecisions.First().Item1.RemoteMovie.Movie;
                var alreadyPending = _repository.AllByMovieId(movie.Id) ?? new List<PendingRelease>();

                foreach (var pair in movieDecisions)
                {
                    var decision = pair.Item1;
                    var reason = pair.Item2;

                    var existingReports = alreadyPending ?? Enumerable.Empty<PendingRelease>();

                    var matchingReports = existingReports.Where(MatchingReleasePredicate(decision.RemoteMovie.Release)).ToList();

                    if (matchingReports.Any())
                    {
                        var matchingReport = matchingReports.First();

                        if (matchingReport.Reason != reason)
                        {
                            if (matchingReport.Reason == PendingReleaseReason.DownloadClientUnavailable)
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, not changing reason", decision.RemoteMovie, matchingReport.Reason);
                            }
                            else
                            {
                                _logger.Debug("The release {0} is already pending with reason {1}, changing to {2}", decision.RemoteMovie, matchingReport.Reason, reason);
                                matchingReport.Reason = reason;
                                _repository.Update(matchingReport);
                            }
                        }
                        else
                        {
                            _logger.Debug("The release {0} is already pending with reason {1}, not adding again", decision.RemoteMovie, reason);
                        }

                        if (matchingReports.Count > 1)
                        {
                            _logger.Debug("The release {0} had {1} duplicate pending, removing duplicates.", decision.RemoteMovie, matchingReports.Count - 1);

                            foreach (var duplicate in matchingReports.Skip(1))
                            {
                                _repository.Delete(duplicate.Id);
                                alreadyPending.Remove(duplicate);
                            }
                        }

                        continue;
                    }

                    _logger.Debug("Adding release {0} to pending releases with reason {1}", decision.RemoteMovie, reason);
                    Insert(decision, reason);
                }
            }
        }

        public List<ReleaseInfo> GetPending()
        {
            var releases = _repository.All().Select(p =>
            {
                var release = p.Release;

                release.PendingReleaseReason = p.Reason;

                return release;
            }).ToList();

            if (releases.Any())
            {
                releases = FilterBlockedIndexers(releases);
            }

            return releases;
        }

        public List<RemoteEpisode> GetPendingRemoteEpisodes(int seriesId)
        {
            return _pendingReleases.Where(p => p.SeriesId == seriesId && p.RemoteEpisode != null).Select(p => p.RemoteEpisode).ToList();
        }

        public List<RemoteMovie> GetPendingRemoteMovies(int movieId)
        {
            return _pendingReleases.Where(p => p.MovieId == movieId && p.RemoteMovie != null).Select(p => p.RemoteMovie).ToList();
        }

        public List<Queue.Queue> GetPendingQueue()
        {
            var queued = new List<Queue.Queue>();
            var movieQueued = new List<Queue.Queue>();
            var nextRssSync = new Lazy<DateTime>(() => _taskManager.GetNextExecution(typeof(RssSyncCommand)));
            var pendingReleases = _pendingReleases.Where(p => p.Reason != PendingReleaseReason.Fallback).ToList();

            var episodeReleases = pendingReleases.Where(p => !IsMovieRelease(p)).ToList();
            var movieReleases = pendingReleases.Where(IsMovieRelease).ToList();

            foreach (var pendingRelease in episodeReleases)
            {
                if (pendingRelease.RemoteEpisode.Episodes.Empty())
                {
                    var noEpisodeItem = GetQueueItem(pendingRelease, nextRssSync, []);

                    noEpisodeItem.ErrorMessage = "Unable to find matching episode(s)";

                    queued.Add(noEpisodeItem);

                    continue;
                }

                queued.Add(GetQueueItem(pendingRelease, nextRssSync, pendingRelease.RemoteEpisode.Episodes));
            }

            // Return best quality release for each episode group, this may result in multiple for the same episode if the episodes in each release differ
            var deduped = queued.Where(q => q.Episodes.Any()).GroupBy(q => q.Episodes.Select(e => e.Id)).Select(g =>
            {
                var series = g.First().Series;

                return g.OrderByDescending(e => e.Quality, new QualityModelComparer(series.QualityProfile))
                        .ThenBy(q => PrioritizeDownloadProtocol(q.Series, q.Protocol))
                        .First();
            }).ToList();

            foreach (var pendingRelease in movieReleases)
            {
                if (pendingRelease.RemoteMovie.Movie == null)
                {
                    var noMovieItem = GetQueueItem(pendingRelease, nextRssSync, (Movie)null);

                    noMovieItem.ErrorMessage = "Unable to find matching movie(s)";

                    movieQueued.Add(noMovieItem);

                    continue;
                }

                movieQueued.Add(GetQueueItem(pendingRelease, nextRssSync, pendingRelease.RemoteMovie.Movie));
            }

            // Return best quality release for each movie
            var dedupedMovies = movieQueued.Where(q => q.Movie != null).GroupBy(q => q.Movie.Id).Select(g =>
            {
                var movie = g.First().Movie;

                return g.OrderByDescending(e => e.Quality, new QualityModelComparer(movie.QualityProfile))
                        .ThenBy(q => PrioritizeDownloadProtocol(q.Movie, q.Protocol))
                        .First();
            });

            return deduped.Concat(dedupedMovies).ToList();
        }

        public List<Queue.Queue> GetPendingQueueObsolete()
        {
            var queued = new List<Queue.Queue>();
            var nextRssSync = new Lazy<DateTime>(() => _taskManager.GetNextExecution(typeof(RssSyncCommand)));
            var pendingReleases = _pendingReleases.Where(p => p.Reason != PendingReleaseReason.Fallback && !IsMovieRelease(p)).ToList();

            foreach (var pendingRelease in pendingReleases)
            {
                if (pendingRelease.RemoteEpisode.Episodes.Empty())
                {
                    var noEpisodeItem = GetQueueItem(pendingRelease, nextRssSync, (Episode)null);

                    noEpisodeItem.ErrorMessage = "Unable to find matching episode(s)";

                    queued.Add(noEpisodeItem);

                    continue;
                }

                foreach (var episode in pendingRelease.RemoteEpisode.Episodes)
                {
                    queued.Add(GetQueueItem(pendingRelease, nextRssSync, episode));
                }
            }

#pragma warning disable CS0612

            // Return best quality release for each episode
            var deduped = queued.Where(q => q.Episode != null).GroupBy(q => q.Episode.Id).Select(g =>
            {
                var series = g.First().Series;

                return g.OrderByDescending(e => e.Quality, new QualityModelComparer(series.QualityProfile))
                    .ThenBy(q => PrioritizeDownloadProtocol(q.Series, q.Protocol))
                    .First();
            });
#pragma warning restore CS0612

            return deduped.ToList();
        }

        public Queue.Queue FindPendingQueueItem(int queueId)
        {
            return GetPendingQueue().SingleOrDefault(p => p.Id == queueId);
        }

        public Queue.Queue FindPendingQueueItemObsolete(int queueId)
        {
            // The obsolete (legacy V3) queue is episode-only; use the same filtered list it
            // is populated from so a movie pending item can never be handed to the
            // episode-only removal path.
            return GetPendingQueueObsolete().SingleOrDefault(p => p.Id == queueId);
        }

        public void RemovePendingQueueItems(int queueId)
        {
            var targetItem = FindPendingRelease(queueId);

            if (IsMovieRelease(targetItem))
            {
                var movieReleases = _repository.AllByMovieId(targetItem.MovieId);

                var movieReleasesToRemove = movieReleases.Where(
                    c => c.ParsedMovieInfo.PrimaryMovieTitle == targetItem.ParsedMovieInfo.PrimaryMovieTitle);

                _repository.DeleteMany(movieReleasesToRemove.Select(c => c.Id));

                return;
            }

            var seriesReleases = _repository.AllBySeriesId(targetItem.SeriesId);

            var releasesToRemove = seriesReleases.Where(
                c => c.ParsedEpisodeInfo.SeasonNumber == targetItem.ParsedEpisodeInfo.SeasonNumber &&
                     c.ParsedEpisodeInfo.EpisodeNumbers.SequenceEqual(targetItem.ParsedEpisodeInfo.EpisodeNumbers));

            _repository.DeleteMany(releasesToRemove.Select(c => c.Id));
        }

        public void RemovePendingQueueItemsObsolete(int queueId)
        {
            var targetItem = FindPendingReleaseObsolete(queueId);
            var seriesReleases = _repository.AllBySeriesId(targetItem.SeriesId);

            var releasesToRemove = seriesReleases.Where(
                c => c.ParsedEpisodeInfo.SeasonNumber == targetItem.ParsedEpisodeInfo.SeasonNumber &&
                     c.ParsedEpisodeInfo.EpisodeNumbers.SequenceEqual(targetItem.ParsedEpisodeInfo.EpisodeNumbers));

            _repository.DeleteMany(releasesToRemove.Select(c => c.Id));
        }

        public RemoteEpisode OldestPendingRelease(int seriesId, int[] episodeIds)
        {
            var seriesReleases = GetPendingReleases(seriesId);

            return seriesReleases.Where(r => r.RemoteEpisode != null)
                                 .Select(r => r.RemoteEpisode)
                                 .Where(r => r.Episodes.Select(e => e.Id).Intersect(episodeIds).Any())
                                 .MaxBy(p => p.Release.AgeHours);
        }

        public RemoteMovie OldestPendingRelease(int movieId)
        {
            var movieReleases = GetPendingReleases(movieId);

            return movieReleases.Where(r => r.RemoteMovie != null)
                                .Select(r => r.RemoteMovie)
                                .MaxBy(p => p.Release.AgeHours);
        }

        private ILookup<int, PendingRelease> CreateEpisodeLookup(IEnumerable<PendingRelease> alreadyPending)
        {
            return alreadyPending.SelectMany(v => v.RemoteEpisode.Episodes
                                                   .Select(d => new { Episode = d, PendingRelease = v }))
                                 .ToLookup(v => v.Episode.Id, v => v.PendingRelease);
        }

        private List<ReleaseInfo> FilterBlockedIndexers(List<ReleaseInfo> releases)
        {
            var blockedIndexers = new HashSet<int>(_indexerStatusService.GetBlockedProviders().Select(v => v.ProviderId));

            return releases.Where(release => !blockedIndexers.Contains(release.IndexerId)).ToList();
        }

        private List<PendingRelease> GetPendingReleases()
        {
            return _pendingReleases;
        }

        private List<PendingRelease> GetPendingReleases(int seriesId)
        {
            return _pendingReleases.Where(p => p.SeriesId == seriesId).ToList();
        }

        private List<PendingRelease> GetPendingMovieReleases(int movieId)
        {
            return _pendingReleases.Where(p => p.MovieId == movieId).ToList();
        }

        private static bool IsMovieRelease(PendingRelease release)
        {
            return release.ParsedMovieInfo != null && release.ParsedEpisodeInfo == null;
        }

        private List<PendingRelease> IncludeRemoteReleases(List<PendingRelease> releases)
        {
            var result = new List<PendingRelease>();

            var episodeReleases = releases.Where(r => !IsMovieRelease(r)).ToList();
            var movieReleases = releases.Where(IsMovieRelease).ToList();

            result.AddRange(IncludeRemoteEpisodes(episodeReleases));

            if (movieReleases.Any())
            {
                result.AddRange(IncludeRemoteMovies(movieReleases));
            }

            return result;
        }

        private List<PendingRelease> IncludeRemoteEpisodes(List<PendingRelease> releases, Dictionary<string, RemoteEpisode> knownRemoteEpisodes = null)
        {
            var result = new List<PendingRelease>();

            if (releases.Empty())
            {
                return result;
            }

            var seriesMap = new Dictionary<int, Series>();

            if (knownRemoteEpisodes != null)
            {
                foreach (var series in knownRemoteEpisodes.Values.Select(v => v.Series))
                {
                    seriesMap.TryAdd(series.Id, series);
                }
            }

            foreach (var series in _seriesService.GetSeries(releases.Select(v => v.SeriesId).Distinct().Where(v => !seriesMap.ContainsKey(v))))
            {
                seriesMap[series.Id] = series;
            }

            foreach (var release in releases)
            {
                var series = seriesMap.GetValueOrDefault(release.SeriesId);

                // Just in case the series was removed, but wasn't cleaned up yet (housekeeper will clean it up)
                if (series == null)
                {
                    continue;
                }

                // Languages will be empty if added before upgrading to v4, reparsing the languages if they're empty will set it to Unknown or better.
                if (release.ParsedEpisodeInfo.Languages.Empty())
                {
                    release.ParsedEpisodeInfo.Languages = LanguageParser.ParseLanguages(release.Title);
                }

                release.RemoteEpisode = new RemoteEpisode
                {
                    Series = series,
                    SeriesMatchType = release.AdditionalInfo?.SeriesMatchType ?? SeriesMatchType.Unknown,
                    ReleaseSource = release.AdditionalInfo?.ReleaseSource ?? ReleaseSourceType.Unknown,
                    ParsedEpisodeInfo = release.ParsedEpisodeInfo,
                    Release = release.Release
                };

                if (knownRemoteEpisodes != null && knownRemoteEpisodes.TryGetValue(release.Release.Title, out var knownRemoteEpisode))
                {
                    release.RemoteEpisode.MappedSeasonNumber = knownRemoteEpisode.MappedSeasonNumber;
                    release.RemoteEpisode.Episodes = knownRemoteEpisode.Episodes;
                }
                else if (ValidateParsedEpisodeInfo.ValidateForSeriesType(release.ParsedEpisodeInfo, series))
                {
                    try
                    {
                        var remoteEpisode = _parsingService.Map(release.ParsedEpisodeInfo, series);

                        release.RemoteEpisode.MappedSeasonNumber = remoteEpisode.MappedSeasonNumber;
                        release.RemoteEpisode.Episodes = remoteEpisode.Episodes;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.Debug(ex, ex.Message);

                        release.RemoteEpisode.MappedSeasonNumber = release.ParsedEpisodeInfo.SeasonNumber;
                        release.RemoteEpisode.Episodes = new List<Episode>();
                    }
                }
                else
                {
                    release.RemoteEpisode.MappedSeasonNumber = release.ParsedEpisodeInfo.SeasonNumber;
                    release.RemoteEpisode.Episodes = new List<Episode>();
                }

                _aggregationService.Augment(release.RemoteEpisode);
                release.RemoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(release.RemoteEpisode, release.Release.Size);

                result.Add(release);
            }

            return result;
        }

        private List<PendingRelease> IncludeRemoteMovies(List<PendingRelease> releases, Dictionary<string, RemoteMovie> knownRemoteMovies = null)
        {
            var result = new List<PendingRelease>();

            var movieMap = new Dictionary<int, Movie>();

            if (knownRemoteMovies != null)
            {
                foreach (var movie in knownRemoteMovies.Values.Select(v => v.Movie))
                {
                    movieMap.TryAdd(movie.Id, movie);
                }
            }

            var movieIds = releases.Select(v => v.MovieId).Distinct().Where(v => !movieMap.ContainsKey(v)).ToList();

            if (movieIds.Any())
            {
                foreach (var movie in _movieService.GetMovies(movieIds) ?? new List<Movie>())
                {
                    movieMap[movie.Id] = movie;
                }
            }

            foreach (var release in releases)
            {
                var movie = movieMap.GetValueOrDefault(release.MovieId);

                // Just in case the movie was removed, but wasn't cleaned up yet (housekeeper will clean it up)
                if (movie == null)
                {
                    continue;
                }

                // Languages will be empty if added before upgrading to v4, reparsing the languages if they're empty will set it to Unknown or better.
                if (release.ParsedMovieInfo.Languages.Empty())
                {
                    release.ParsedMovieInfo.Languages = LanguageParser.ParseLanguages(release.Title);
                }

                release.RemoteMovie = new RemoteMovie
                {
                    Movie = movie,
                    MovieMatchType = release.AdditionalInfo?.MovieMatchType ?? MovieMatchType.Unknown,
                    ReleaseSource = release.AdditionalInfo?.ReleaseSource ?? ReleaseSourceType.Unknown,
                    ParsedMovieInfo = release.ParsedMovieInfo,
                    Release = release.Release
                };

                if (knownRemoteMovies != null && knownRemoteMovies.TryGetValue(release.Release.Title, out var knownRemoteMovie))
                {
                    release.RemoteMovie.Movie = knownRemoteMovie.Movie;
                }

                _movieAggregationService.Augment(release.RemoteMovie);
                release.RemoteMovie.CustomFormats = _formatCalculator.ParseCustomFormat(release.RemoteMovie, release.Release.Size);

                result.Add(release);
            }

            return result;
        }

        private Queue.Queue GetQueueItem(PendingRelease pendingRelease, Lazy<DateTime> nextRssSync, List<Episode> episodes)
        {
            var ect = pendingRelease.Release.PublishDate.AddMinutes(GetDelay(pendingRelease.RemoteEpisode));

            if (ect < nextRssSync.Value)
            {
                ect = nextRssSync.Value;
            }
            else
            {
                ect = ect.AddMinutes(_configService.RssSyncInterval);
            }

            var timeLeft = ect.Subtract(DateTime.UtcNow);

            if (timeLeft.TotalSeconds < 0)
            {
                timeLeft = TimeSpan.Zero;
            }

            string downloadClientName = null;
            var indexer = _indexerFactory.Find(pendingRelease.Release.IndexerId);

            if (indexer is { DownloadClientId: > 0 })
            {
                var downloadClient = _downloadClientFactory.Find(indexer.DownloadClientId);

                downloadClientName = downloadClient?.Name;
            }

            var queue = new Queue.Queue
            {
                Id = GetQueueId(pendingRelease),
                Series = pendingRelease.RemoteEpisode.Series,
                Episodes = episodes,
                Languages = pendingRelease.RemoteEpisode.Languages,
                Quality = pendingRelease.RemoteEpisode.ParsedEpisodeInfo.Quality,
                Title = pendingRelease.Title,
                Size = pendingRelease.RemoteEpisode.Release.Size,
                SizeLeft = pendingRelease.RemoteEpisode.Release.Size,
                RemoteEpisode = pendingRelease.RemoteEpisode,
                TimeLeft = timeLeft,
                EstimatedCompletionTime = ect,
                Added = pendingRelease.Added,
                Status = Enum.TryParse(pendingRelease.Reason.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                Protocol = pendingRelease.RemoteEpisode.Release.DownloadProtocol,
                Indexer = pendingRelease.RemoteEpisode.Release.Indexer,
                DownloadClient = downloadClientName
            };

            return queue;
        }

        private Queue.Queue GetQueueItem(PendingRelease pendingRelease, Lazy<DateTime> nextRssSync, Episode episode)
        {
            var ect = pendingRelease.Release.PublishDate.AddMinutes(GetDelay(pendingRelease.RemoteEpisode));

            if (ect < nextRssSync.Value)
            {
                ect = nextRssSync.Value;
            }
            else
            {
                ect = ect.AddMinutes(_configService.RssSyncInterval);
            }

            var timeLeft = ect.Subtract(DateTime.UtcNow);

            if (timeLeft.TotalSeconds < 0)
            {
                timeLeft = TimeSpan.Zero;
            }

            string downloadClientName = null;
            var indexer = _indexerFactory.Find(pendingRelease.Release.IndexerId);

            if (indexer is { DownloadClientId: > 0 })
            {
                var downloadClient = _downloadClientFactory.Find(indexer.DownloadClientId);

                downloadClientName = downloadClient?.Name;
            }

            var queue = new Queue.Queue
            {
                Id = GetQueueId(pendingRelease, episode),
                Series = pendingRelease.RemoteEpisode.Series,

#pragma warning disable CS0612
                Episode = episode,
#pragma warning restore CS0612

                Languages = pendingRelease.RemoteEpisode.Languages,
                Quality = pendingRelease.RemoteEpisode.ParsedEpisodeInfo.Quality,
                Title = pendingRelease.Title,
                Size = pendingRelease.RemoteEpisode.Release.Size,
                SizeLeft = pendingRelease.RemoteEpisode.Release.Size,
                RemoteEpisode = pendingRelease.RemoteEpisode,
                TimeLeft = timeLeft,
                EstimatedCompletionTime = ect,
                Added = pendingRelease.Added,
                Status = Enum.TryParse(pendingRelease.Reason.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                Protocol = pendingRelease.RemoteEpisode.Release.DownloadProtocol,
                Indexer = pendingRelease.RemoteEpisode.Release.Indexer,
                DownloadClient = downloadClientName
            };

            return queue;
        }

        private Queue.Queue GetQueueItem(PendingRelease pendingRelease, Lazy<DateTime> nextRssSync, Movie movie)
        {
            var ect = pendingRelease.Release.PublishDate.AddMinutes(GetDelay(pendingRelease.RemoteMovie));

            if (ect < nextRssSync.Value)
            {
                ect = nextRssSync.Value;
            }
            else
            {
                ect = ect.AddMinutes(_configService.RssSyncInterval);
            }

            var timeLeft = ect.Subtract(DateTime.UtcNow);

            if (timeLeft.TotalSeconds < 0)
            {
                timeLeft = TimeSpan.Zero;
            }

            string downloadClientName = null;
            var indexer = _indexerFactory.Find(pendingRelease.Release.IndexerId);

            if (indexer is { DownloadClientId: > 0 })
            {
                var downloadClient = _downloadClientFactory.Find(indexer.DownloadClientId);

                downloadClientName = downloadClient?.Name;
            }

            var queue = new Queue.Queue
            {
                Id = GetQueueId(pendingRelease, movie),
                Movie = movie,
                Quality = pendingRelease.RemoteMovie.ParsedMovieInfo?.Quality ?? new QualityModel(),
                Languages = pendingRelease.RemoteMovie.Languages,
                Title = pendingRelease.Title,
                Size = pendingRelease.RemoteMovie.Release.Size,
                SizeLeft = pendingRelease.RemoteMovie.Release.Size,
                RemoteMovie = pendingRelease.RemoteMovie,
                TimeLeft = timeLeft,
                EstimatedCompletionTime = ect,
                Added = pendingRelease.Added,
                Status = Enum.TryParse(pendingRelease.Reason.ToString(), out QueueStatus outValue) ? outValue : QueueStatus.Unknown,
                Protocol = pendingRelease.RemoteMovie.Release.DownloadProtocol,
                Indexer = pendingRelease.RemoteMovie.Release.Indexer,
                DownloadClient = downloadClientName
            };

            return queue;
        }

        private void Insert(DownloadDecision decision, PendingReleaseReason reason)
        {
            if (decision.RemoteMovie != null)
            {
                InsertMovie(decision.RemoteMovie, reason);

                return;
            }

            InsertEpisode(decision.RemoteEpisode, reason);
        }

        private void InsertEpisode(RemoteEpisode remoteEpisode, PendingReleaseReason reason)
        {
            _repository.Insert(new PendingRelease
            {
                SeriesId = remoteEpisode.Series.Id,
                ParsedEpisodeInfo = remoteEpisode.ParsedEpisodeInfo,
                Release = remoteEpisode.Release,
                Title = remoteEpisode.Release.Title,
                Added = DateTime.UtcNow,
                Reason = reason,
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    SeriesMatchType = remoteEpisode.SeriesMatchType,
                    ReleaseSource = remoteEpisode.ReleaseSource
                }
            });

            _eventAggregator.PublishEvent(new PendingReleasesUpdatedEvent());
        }

        private void InsertMovie(RemoteMovie remoteMovie, PendingReleaseReason reason)
        {
            var release = new PendingRelease
            {
                MovieId = remoteMovie.Movie.Id,
                ParsedMovieInfo = remoteMovie.ParsedMovieInfo,
                Release = remoteMovie.Release,
                Title = remoteMovie.Release.Title,
                Added = DateTime.UtcNow,
                Reason = reason,
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    MovieMatchType = remoteMovie.MovieMatchType,
                    ReleaseSource = remoteMovie.ReleaseSource
                }
            };

            if (release.ParsedMovieInfo == null)
            {
                _logger.Warn("Pending release {0} does not have ParsedMovieInfo, will cause issues.", release.Title);
            }

            _repository.Insert(release);

            _eventAggregator.PublishEvent(new PendingReleasesUpdatedEvent());
        }

        private void Delete(PendingRelease pendingRelease)
        {
            _repository.Delete(pendingRelease);
            _eventAggregator.PublishEvent(new PendingReleasesUpdatedEvent());
        }

        private int GetDelay(RemoteEpisode remoteEpisode)
        {
            var delayProfile = _delayProfileService.AllForTags(remoteEpisode.Series.Tags).OrderBy(d => d.Order).First();
            var delay = delayProfile.GetProtocolDelay(remoteEpisode.Release.DownloadProtocol);
            var minimumAge = _configService.MinimumAge;

            return new[] { delay, minimumAge }.Max();
        }

        private int GetDelay(RemoteMovie remoteMovie)
        {
            var delayProfile = _delayProfileService.AllForTags(remoteMovie.Movie.Tags).OrderBy(d => d.Order).First();
            var delay = delayProfile.GetProtocolDelay(remoteMovie.Release.DownloadProtocol);
            var minimumAge = _configService.MinimumAge;

            return new[] { delay, minimumAge }.Max();
        }

        private void RemoveGrabbed(RemoteMovie remoteMovie)
        {
            var pendingReleases = GetPendingMovieReleases(remoteMovie.Movie.Id);

            var existingReports = pendingReleases.Where(r => r.RemoteMovie != null && r.RemoteMovie.Movie.Id == remoteMovie.Movie.Id)
                                                             .ToList();

            if (existingReports.Empty())
            {
                return;
            }

            var profile = remoteMovie.Movie.QualityProfile;

            foreach (var existingReport in existingReports)
            {
                var compare = new QualityModelComparer(profile).Compare(remoteMovie.ParsedMovieInfo.Quality,
                                                                        existingReport.RemoteMovie.ParsedMovieInfo.Quality);

                // Only remove lower/equal quality pending releases
                // It is safer to retry these releases on the next round than remove it and try to re-add it (if its still in the feed)
                if (compare >= 0)
                {
                    _logger.Debug("Removing previously pending release, as it was grabbed.");
                    Delete(existingReport);
                }
            }
        }

        private void RemoveGrabbed(RemoteEpisode remoteEpisode)
        {
            var pendingReleases = GetPendingReleases(remoteEpisode.Series.Id);
            var episodeIds = remoteEpisode.Episodes.Select(e => e.Id);

            var existingReports = pendingReleases.Where(r => r.RemoteEpisode != null && r.RemoteEpisode.Episodes.Select(e => e.Id)
                                                             .Intersect(episodeIds)
                                                             .Any())
                                                             .ToList();

            if (existingReports.Empty())
            {
                return;
            }

            var profile = remoteEpisode.Series.QualityProfile;

            foreach (var existingReport in existingReports)
            {
                var compare = new QualityModelComparer(profile).Compare(remoteEpisode.ParsedEpisodeInfo.Quality,
                                                                        existingReport.RemoteEpisode.ParsedEpisodeInfo.Quality);

                // Only remove lower/equal quality pending releases
                // It is safer to retry these releases on the next round than remove it and try to re-add it (if its still in the feed)
                if (compare >= 0)
                {
                    _logger.Debug("Removing previously pending release, as it was grabbed.");
                    Delete(existingReport);
                }
            }
        }

        private void RemoveRejected(List<DownloadDecision> rejected)
        {
            _logger.Debug("Removing failed releases from pending");
            var pending = GetPendingReleases();

            foreach (var rejectedRelease in rejected)
            {
                var release = rejectedRelease.RemoteMovie?.Release ?? rejectedRelease.RemoteEpisode?.Release;

                if (release == null)
                {
                    continue;
                }

                var matching = pending.Where(MatchingReleasePredicate(release));

                foreach (var pendingRelease in matching)
                {
                    _logger.Debug("Removing previously pending release, as it has now been rejected.");
                    Delete(pendingRelease);
                }
            }
        }

        private PendingRelease FindPendingRelease(int queueId)
        {
            return GetPendingReleases().First(p =>
                GetQueueId(p) == queueId ||
                (p.RemoteMovie?.Movie != null && GetQueueId(p, p.RemoteMovie.Movie) == queueId));
        }

        private PendingRelease FindPendingReleaseObsolete(int queueId)
        {
            return GetPendingReleases().First(p => p.RemoteEpisode != null && p.RemoteEpisode.Episodes.Any(e => queueId == GetQueueId(p, e)));
        }

        private int GetQueueId(PendingRelease pendingRelease)
        {
            return HashConverter.GetHashInt31(string.Format("pending-{0}", pendingRelease.Id));
        }

        private int GetQueueId(PendingRelease pendingRelease, Episode episode)
        {
            return HashConverter.GetHashInt31(string.Format("pending-{0}-ep{1}", pendingRelease.Id, episode?.Id ?? 0));
        }

        private int GetQueueId(PendingRelease pendingRelease, Movie movie)
        {
            return HashConverter.GetHashInt31(string.Format("pending-{0}-movie{1}", pendingRelease.Id, movie?.Id ?? 0));
        }

        private int PrioritizeDownloadProtocol(Series series, DownloadProtocol downloadProtocol)
        {
            var delayProfile = _delayProfileService.BestForTags(series.Tags);

            if (downloadProtocol == delayProfile.PreferredProtocol)
            {
                return 0;
            }

            return 1;
        }

        private int PrioritizeDownloadProtocol(Movie movie, DownloadProtocol downloadProtocol)
        {
            var delayProfile = _delayProfileService.BestForTags(movie.Tags);

            if (downloadProtocol == delayProfile.PreferredProtocol)
            {
                return 0;
            }

            return 1;
        }

        private void UpdatePendingReleases()
        {
            _pendingReleases = IncludeRemoteReleases(_repository.All().ToList());
        }

        public void Handle(SeriesEditedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(SeriesUpdatedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(SeriesDeletedEvent message)
        {
            _repository.DeleteBySeriesIds(message.Series.Select(m => m.Id).ToList());
            UpdatePendingReleases();
        }

        public void Handle(MoviesDeletedEvent message)
        {
            _repository.DeleteByMovieIds(message.Movies.Select(m => m.Id).ToList());
            UpdatePendingReleases();
        }

        public void Handle(EpisodeGrabbedEvent message)
        {
            RemoveGrabbed(message.Episode);
            UpdatePendingReleases();
        }

        public void Handle(MovieGrabbedEvent message)
        {
            RemoveGrabbed(message.Movie);
            UpdatePendingReleases();
        }

        public void Handle(RssSyncCompleteEvent message)
        {
            RemoveRejected(message.ProcessedDecisions.Rejected);
            UpdatePendingReleases();
        }

        public void Handle(QualityProfileUpdatedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(ApplicationStartedEvent message)
        {
            UpdatePendingReleases();
        }

        public void Handle(ConfigSavedEvent message)
        {
            UpdatePendingReleases();
        }

        private static Func<PendingRelease, bool> MatchingReleasePredicate(ReleaseInfo release)
        {
            return p => p.Title == release.Title &&
                        p.Release.PublishDate == release.PublishDate &&
                        p.Release.Indexer == release.Indexer;
        }
    }
}
