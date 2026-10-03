using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;
using Sonarr.Http;
using Sonarr.Http.Exceptions;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.MediaCompression
{
    public class ReprobeResource
    {
        // Nullable so a present-but-empty body ({}) is distinguishable from an absent body; both
        // are treated as "force" (see Reprobe), while an explicit force:false uses the cache.
        public bool? Force { get; set; }
    }

    public class TranscodeHistoryClearedResource
    {
        public int ClearedCount { get; set; }
    }

    public class TranscodeBulkResolveFailureResource
    {
        public int JobId { get; set; }
        public string Error { get; set; }
    }

    // A bulk resolve reports the resolved jobs and, separately, the ids that could not be resolved
    // with the reason (instead of the old behaviour of silently dropping the failures).
    public class TranscodeBulkResolveResource
    {
        public List<TranscodeJobResource> Resolved { get; set; }
        public List<TranscodeBulkResolveFailureResource> Failed { get; set; }
    }

    // The API-level 409 used by DELETE /jobs/{id} for a job that exists but is not cancellable.
    public class ConflictException : ApiException
    {
        public ConflictException(object content = null)
            : base(HttpStatusCode.Conflict, content)
        {
        }
    }

    [V3ApiController("media-compression")]
    [V5ApiController("media-compression")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class MediaCompressionController : RestController<MediaCompressionCapabilitiesResource>
    {
        // Guards the T13 dedup check + queue insert so two concurrent POSTs cannot both observe the
        // same active set and double-queue the same file. Controllers are per-request, so this has
        // to be static to serialise across requests in a single process.
        private static readonly object QueueLock = new object();

        private static readonly StringComparer SourcePathComparer =
            OsInfo.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private readonly IGpuCapabilityService _gpuCapabilityService;
        private readonly ITranscodeService _transcodeService;
        private readonly ITranscodeJobRepository _transcodeJobRepository;
        private readonly ITranscodeProfileService _transcodeProfileService;
        private readonly IConfigService _configService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMovieFileService _movieFileService;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly IManageCommandQueue _commandQueueManager;

        public MediaCompressionController(IGpuCapabilityService gpuCapabilityService,
                                          ITranscodeService transcodeService,
                                          ITranscodeJobRepository transcodeJobRepository,
                                          ITranscodeProfileService transcodeProfileService,
                                          IConfigService configService,
                                          IMediaFileService mediaFileService,
                                          IMovieFileService movieFileService,
                                          ISeriesService seriesService,
                                          IMovieService movieService,
                                          IManageCommandQueue commandQueueManager)
        {
            _gpuCapabilityService = gpuCapabilityService;
            _transcodeService = transcodeService;
            _transcodeJobRepository = transcodeJobRepository;
            _transcodeProfileService = transcodeProfileService;
            _configService = configService;
            _mediaFileService = mediaFileService;
            _movieFileService = movieFileService;
            _seriesService = seriesService;
            _movieService = movieService;
            _commandQueueManager = commandQueueManager;
        }

        [NonAction]
        public override Results<Ok<MediaCompressionCapabilitiesResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            throw new NotImplementedException();
        }

        protected override MediaCompressionCapabilitiesResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet("capabilities")]
        [Produces("application/json")]
        public Ok<MediaCompressionCapabilitiesResource> GetCapabilities()
        {
            return TypedResults.Ok(_gpuCapabilityService.GetCapabilities().ToResource());
        }

        [HttpPut("devices")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<List<TranscodeDeviceResource>> UpdateDevices([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] List<TranscodeDeviceResource> devices)
        {
            if (devices == null)
            {
                throw new BadRequestException("Request body must be provided");
            }

            ValidateDevices(devices);

            var updated = _gpuCapabilityService.UpdateDevices(devices.ToModel());

            // A newly enabled device or higher max-parallel should start queued jobs right away.
            _transcodeService.Wake();

            return TypedResults.Ok(updated.ToResource());
        }

        [HttpPost("capabilities/reprobe")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<MediaCompressionCapabilitiesResource> Reprobe([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReprobeResource resource)
        {
            // Reprobe is an explicit user action, so it forces a fresh probe unless the caller
            // explicitly asks for the cached snapshot (force:false).
            return TypedResults.Ok(_gpuCapabilityService.GetCapabilities(resource?.Force ?? true).ToResource());
        }

        [HttpGet("profiles")]
        [Produces("application/json")]
        public Ok<List<TranscodeProfileResource>> GetProfiles()
        {
            return TypedResults.Ok(_transcodeProfileService.GetAll().ToResource());
        }

        [HttpPost("profiles")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<TranscodeProfileResource> AddProfile([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TranscodeProfileResource resource)
        {
            if (resource == null)
            {
                throw new BadRequestException("Request body must be provided");
            }

            ValidateProfile(resource, id: null);

            var profile = resource.ToModel();
            profile.Id = 0;

            return TypedResults.Ok(_transcodeProfileService.Add(profile).ToResource());
        }

        [HttpPut("profiles/{id:int}")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<TranscodeProfileResource> UpdateProfile(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TranscodeProfileResource resource)
        {
            if (resource == null)
            {
                throw new BadRequestException("Request body must be provided");
            }

            if (_transcodeProfileService.Get(id) == null)
            {
                throw new NotFoundException($"Transcode profile {id} was not found");
            }

            ValidateProfile(resource, id);

            var profile = resource.ToModel();
            profile.Id = id;

            return TypedResults.Ok(_transcodeProfileService.Update(profile).ToResource());
        }

        [HttpDelete("profiles/{id:int}")]
        public Ok DeleteProfile(int id)
        {
            _transcodeProfileService.Delete(id);

            return TypedResults.Ok();
        }

        [HttpGet("jobs")]
        [Produces("application/json")]
        public Ok<List<TranscodeJobResource>> GetJobs()
        {
            return TypedResults.Ok(ToJobResources(_transcodeJobRepository.GetRecentIncludingActive(100)));
        }

        // Enriches the flat job rows with the series/season (or movie) they belong to so the SPA can
        // group the queue. Batched per distinct id so one page load is a handful of queries, not one
        // per row.
        private List<TranscodeJobResource> ToJobResources(List<TranscodeJob> jobs)
        {
            var resources = jobs.ToResource();

            var seriesIds = jobs.Where(job => job.SeriesId.HasValue)
                                .Select(job => job.SeriesId.Value)
                                .Distinct()
                                .ToList();

            var episodeFileIds = jobs.Where(job => job.EpisodeFileId.HasValue)
                                     .Select(job => job.EpisodeFileId.Value)
                                     .Distinct()
                                     .ToList();

            var movieIds = jobs.Where(job => job.MovieId.HasValue)
                               .Select(job => job.MovieId.Value)
                               .Distinct()
                               .ToList();

            var seriesTitles = seriesIds.Any()
                ? _seriesService.GetSeries(seriesIds).ToDictionary(series => series.Id, series => series.Title)
                : new Dictionary<int, string>();

            var seasonNumbers = episodeFileIds.Any()
                ? _mediaFileService.Get(episodeFileIds).ToDictionary(file => file.Id, file => file.SeasonNumber)
                : new Dictionary<int, int>();

            var movieTitles = movieIds.Any()
                ? _movieService.GetMovies(movieIds).ToDictionary(movie => movie.Id, movie => movie.Title)
                : new Dictionary<int, string>();

            foreach (var resource in resources)
            {
                if (resource.SeriesId.HasValue)
                {
                    resource.SeriesTitle = seriesTitles.GetValueOrDefault(resource.SeriesId.Value);
                }

                if (resource.EpisodeFileId.HasValue && seasonNumbers.TryGetValue(resource.EpisodeFileId.Value, out var seasonNumber))
                {
                    resource.SeasonNumber = seasonNumber;
                }

                if (resource.MovieId.HasValue)
                {
                    resource.MovieTitle = movieTitles.GetValueOrDefault(resource.MovieId.Value);
                }
            }

            return resources;
        }

        [HttpPost("jobs")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<TranscodeJobsResource> CreateJobs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TranscodeJobRequestResource request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request body must be provided");
            }

            return CreateJobsInternal(request);
        }

        private Ok<TranscodeJobsResource> CreateJobsInternal(TranscodeJobRequestResource request)
        {
            // Resolve the profile non-throwing: a stale profileId must not 404 a request that has
            // nothing to queue. It is validated only once we know there is actually work to do.
            var profile = request.ProfileId.HasValue ? _transcodeProfileService.Find(request.ProfileId.Value) : null;

            var effectiveMode = ResolveEffectiveMode(request, profile);

            if (effectiveMode == TranscodeMode.Remux)
            {
                ValidateContainer(request.Container);
            }

            ValidatePreset(request.Preset);

            // Requeue tracking: when retrying specific history jobs, resolve their files here (the
            // file/series id lists are ignored) and drop any already requeued, so a repeated click
            // cannot queue the same file twice.
            var requeueSources = ResolveRequeueSources(request);

            if (HasRequeueJobIds(request))
            {
                request.EpisodeFileIds = requeueSources.Where(job => job.EpisodeFileId.HasValue)
                                                       .Select(job => job.EpisodeFileId.Value)
                                                       .Distinct()
                                                       .ToList();
                request.MovieFileIds = requeueSources.Where(job => job.MovieFileId.HasValue)
                                                     .Select(job => job.MovieFileId.Value)
                                                     .Distinct()
                                                     .ToList();
                request.SeriesIds = null;
                request.SeasonNumbers = null;
                request.MovieIds = null;
            }

            // A requeue reuses the source job's settings (profile, mode, codec, target, device, …);
            // look the source up by file id so BuildJob can clone it instead of falling back to the
            // request/defaults.
            var requeueEpisodeSources = requeueSources
                .Where(job => job.EpisodeFileId.HasValue)
                .GroupBy(job => job.EpisodeFileId.Value)
                .ToDictionary(group => group.Key, group => group.First());

            var requeueMovieSources = requeueSources
                .Where(job => job.MovieFileId.HasValue)
                .GroupBy(job => job.MovieFileId.Value)
                .ToDictionary(group => group.Key, group => group.First());

            // The expensive per-file/per-series reads happen outside the queue lock; only the
            // dedup check + insert window is serialised.
            var candidates = ResolveCandidates(request, out var requestedCount);

            List<TranscodeJob> created;

            // Serialise the dedup-check + insert window so two concurrent requests cannot both
            // observe the same active set and double-queue the same file/path.
            lock (QueueLock)
            {
                var activeJobs = _transcodeJobRepository.GetActive() ?? new List<TranscodeJob>();
                var activeEpisodeFileIds = new HashSet<int>(activeJobs
                    .Where(job => job.EpisodeFileId.HasValue)
                    .Select(job => job.EpisodeFileId.Value));
                var activeMovieFileIds = new HashSet<int>(activeJobs
                    .Where(job => job.MovieFileId.HasValue)
                    .Select(job => job.MovieFileId.Value));
                var activeSourcePaths = new HashSet<string>(
                    activeJobs.Where(job => job.SourcePath.IsNotNullOrWhiteSpace())
                              .Select(job => NormalizeSourcePath(job.SourcePath)),
                    SourcePathComparer);

                // Tracks what this request has already planned so a duplicate within the same POST is
                // always collapsed, even when Force bypasses the persisted active-job check.
                var plannedEpisodeFileIds = new HashSet<int>();
                var plannedMovieFileIds = new HashSet<int>();
                var plannedSourcePaths = new HashSet<string>(SourcePathComparer);

                var jobs = new List<TranscodeJob>();

                foreach (var candidate in candidates)
                {
                    var normalizedPath = candidate.Path.IsNotNullOrWhiteSpace() ? NormalizeSourcePath(candidate.Path) : null;

                    if (candidate.MediaType == MediaType.Series)
                    {
                        if (plannedEpisodeFileIds.Contains(candidate.File.Id) ||
                            (normalizedPath != null && plannedSourcePaths.Contains(normalizedPath)))
                        {
                            continue;
                        }

                        if (!request.Force &&
                            (activeEpisodeFileIds.Contains(candidate.File.Id) ||
                             (normalizedPath != null && activeSourcePaths.Contains(normalizedPath))))
                        {
                            continue;
                        }

                        plannedEpisodeFileIds.Add(candidate.File.Id);
                    }
                    else
                    {
                        if (plannedMovieFileIds.Contains(candidate.File.Id) ||
                            (normalizedPath != null && plannedSourcePaths.Contains(normalizedPath)))
                        {
                            continue;
                        }

                        if (!request.Force &&
                            (activeMovieFileIds.Contains(candidate.File.Id) ||
                             (normalizedPath != null && activeSourcePaths.Contains(normalizedPath))))
                        {
                            continue;
                        }

                        plannedMovieFileIds.Add(candidate.File.Id);
                    }

                    if (normalizedPath != null)
                    {
                        plannedSourcePaths.Add(normalizedPath);
                    }

                    TranscodeJob source = null;

                    if (candidate.MediaType == MediaType.Series)
                    {
                        requeueEpisodeSources.TryGetValue(candidate.File.Id, out source);
                    }
                    else
                    {
                        requeueMovieSources.TryGetValue(candidate.File.Id, out source);
                    }

                    jobs.Add(BuildJob(candidate.MediaType, candidate.File, candidate.Path, request, profile, candidate.SeriesId, candidate.MovieId, source));
                }

                // Only now that we know there is real work does a stale profileId become an error.
                if (request.ProfileId.HasValue && profile == null && jobs.Any(job => job.SourcePath.IsNotNullOrWhiteSpace()))
                {
                    throw new NotFoundException($"Transcode profile {request.ProfileId.Value} was not found");
                }

                created = jobs
                    .Where(job => job.SourcePath.IsNotNullOrWhiteSpace())
                    .Select(job => _transcodeService.Queue(job))
                    .ToList();

                // Stamp each retried source job with the job it became so the UI can disable its
                // requeue button (and Requeue All Failed skips it).
                foreach (var source in requeueSources)
                {
                    var successor = created.FirstOrDefault(job => IsSuccessorOf(job, source));

                    if (successor != null)
                    {
                        source.RequeuedJobId = successor.Id;
                        _transcodeJobRepository.Update(source);
                    }
                }
            }

            if (created.Count > 0)
            {
                _transcodeService.Wake();
                _commandQueueManager.Push(new TranscodeMediaCommand(), CommandPriority.Normal, CommandTrigger.Manual);
            }

            var projectedSavings = created.Sum(ProjectedSavings);
            var skippedCount = Math.Max(0, requestedCount - created.Count);

            return TypedResults.Ok(new TranscodeJobsResource
            {
                Jobs = created.ToResource(),
                ProjectedSavingsBytes = projectedSavings,
                RequestedCount = requestedCount,
                SkippedCount = skippedCount
            });
        }

        private static bool HasRequeueJobIds(TranscodeJobRequestResource request)
        {
            return request.RequeueJobIds != null && request.RequeueJobIds.Count > 0;
        }

        private List<TranscodeJob> ResolveRequeueSources(TranscodeJobRequestResource request)
        {
            var sources = new List<TranscodeJob>();

            if (!HasRequeueJobIds(request))
            {
                return sources;
            }

            // Find (not Get): a stale id (e.g. after Clear History) must be skipped, not throw.
            foreach (var id in request.RequeueJobIds)
            {
                var job = _transcodeJobRepository.Find(id);

                if (job != null &&
                    job.RequeuedJobId == null &&
                    (job.EpisodeFileId.HasValue || job.MovieFileId.HasValue))
                {
                    sources.Add(job);
                }
            }

            return sources;
        }

        private static bool IsSuccessorOf(TranscodeJob created, TranscodeJob source)
        {
            if (source.EpisodeFileId.HasValue && created.EpisodeFileId == source.EpisodeFileId)
            {
                return true;
            }

            return source.MovieFileId.HasValue && created.MovieFileId == source.MovieFileId;
        }

        // A resolved file that this request may transcode. Resolution (the per-file and per-parent
        // DB reads) happens before the queue lock; the lock then only applies the active/planned
        // dedup and inserts.
        private sealed class TranscodeCandidate
        {
            public MediaType MediaType { get; set; }
            public MediaFileBase File { get; set; }
            public string Path { get; set; }
            public int? SeriesId { get; set; }
            public int? MovieId { get; set; }
        }

        private List<TranscodeCandidate> ResolveCandidates(TranscodeJobRequestResource request, out int requestedCount)
        {
            var episodeFileIds = new HashSet<int>(request.EpisodeFileIds ?? new List<int>());
            var movieFileIds = new HashSet<int>(request.MovieFileIds ?? new List<int>());
            var seasonNumbers = new HashSet<int>(request.SeasonNumbers ?? new List<int>());

            foreach (var seriesId in request.SeriesIds ?? new List<int>())
            {
                foreach (var file in _mediaFileService.GetFilesBySeries(seriesId))
                {
                    if (seasonNumbers.Count > 0 && !seasonNumbers.Contains(file.SeasonNumber))
                    {
                        continue;
                    }

                    episodeFileIds.Add(file.Id);
                }
            }

            foreach (var movieId in request.MovieIds ?? new List<int>())
            {
                foreach (var file in _movieFileService.GetFilesByMovie(movieId))
                {
                    movieFileIds.Add(file.Id);
                }
            }

            requestedCount = episodeFileIds.Count + movieFileIds.Count;

            var candidates = new List<TranscodeCandidate>();

            foreach (var id in episodeFileIds)
            {
                EpisodeFile file;

                try
                {
                    file = _mediaFileService.Get(id);
                }
                catch (ModelNotFoundException)
                {
                    // The file was removed between selection and queueing; skip it rather than
                    // aborting the whole request.
                    continue;
                }

                if (file == null)
                {
                    continue;
                }

                NzbDrone.Core.Tv.Series series;

                try
                {
                    series = _seriesService.GetSeries(file.SeriesId);
                }
                catch (ModelNotFoundException)
                {
                    series = null;
                }

                var path = series == null ? null : Path.Combine(series.Path, file.RelativePath);

                candidates.Add(new TranscodeCandidate
                {
                    MediaType = MediaType.Series,
                    File = file,
                    Path = path,
                    SeriesId = file.SeriesId,
                    MovieId = null
                });
            }

            foreach (var id in movieFileIds)
            {
                MovieFile file;

                try
                {
                    file = _movieFileService.GetMovie(id);
                }
                catch (ModelNotFoundException)
                {
                    continue;
                }

                if (file == null)
                {
                    continue;
                }

                Movie movie;

                try
                {
                    movie = _movieService.GetMovie(file.MovieId);
                }
                catch (ModelNotFoundException)
                {
                    movie = null;
                }

                var path = movie == null ? null : Path.Combine(movie.Path, file.RelativePath);

                candidates.Add(new TranscodeCandidate
                {
                    MediaType = MediaType.Movie,
                    File = file,
                    Path = path,
                    SeriesId = null,
                    MovieId = file.MovieId
                });
            }

            return candidates;
        }

        private TranscodeMode ResolveEffectiveMode(TranscodeJobRequestResource request, TranscodeProfile profile)
        {
            if (request.Mode.IsNotNullOrWhiteSpace())
            {
                return ParseMode(request.Mode);
            }

            if (profile != null)
            {
                return profile.Mode;
            }

            return _configService.DefaultRateControlMode;
        }

        // PercentageReduction has no explicit target size, but its saving is implied by the target
        // percent. Quality/Remux have no projected target, so they report 0.
        private static long ProjectedSavings(TranscodeJob job)
        {
            var sourceSize = job.SourceSize ?? 0;

            var targetSize = job.Mode switch
            {
                TranscodeMode.TargetSize => job.TargetSize,
                TranscodeMode.PercentageReduction when job.TargetPercent.HasValue => (long)(sourceSize * (job.TargetPercent.Value / 100.0)),
                _ => null
            };

            return Math.Max(0, sourceSize - (targetSize ?? sourceSize));
        }

        [HttpGet("jobs/blocked")]
        [Produces("application/json")]
        public Ok<List<TranscodeJobResource>> GetBlockedJobs()
        {
            return TypedResults.Ok(ToJobResources(_transcodeService.GetJobsExceedingSettings()));
        }

        [HttpPost("jobs/force-stop")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<List<TranscodeJobResource>> ForceStopJobs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ForceStopTranscodeJobsResource request)
        {
            var jobIds = request?.JobIds;

            if (jobIds == null || jobIds.Count == 0)
            {
                jobIds = _transcodeService.GetJobsExceedingSettings().Select(job => job.Id).ToList();
            }

            return TypedResults.Ok(_transcodeService.ForceStopJobs(jobIds).ToResource());
        }

        [HttpDelete("jobs/{id:int}")]
        public Ok CancelJob(int id)
        {
            switch (_transcodeService.CancelJob(id))
            {
                case CancelJobResult.NotFound:
                    throw new NotFoundException($"Transcode job {id} was not found");
                case CancelJobResult.NotCancellable:
                    throw new ConflictException($"Transcode job {id} is not in a cancellable state");
                default:
                    return TypedResults.Ok();
            }
        }

        [HttpDelete("jobs/history")]
        [Produces("application/json")]
        public Ok<TranscodeHistoryClearedResource> ClearHistory()
        {
            return TypedResults.Ok(new TranscodeHistoryClearedResource
            {
                ClearedCount = _transcodeService.ClearHistory()
            });
        }

        [HttpPost("jobs/{id:int}/resolve")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<TranscodeJobResource> ResolveJob(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ResolveTranscodeJobResource request)
        {
            var action = ParseAction(request?.Action);

            return TypedResults.Ok(_transcodeService.Resolve(id, action).ToResource());
        }

        [HttpPost("jobs/bulk-resolve")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<TranscodeBulkResolveResource> BulkResolveJobs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] BulkResolveTranscodeJobsResource request)
        {
            if (request == null)
            {
                throw new BadRequestException("Request body must be provided");
            }

            if (request.JobIds == null || request.JobIds.Count == 0)
            {
                throw new BadRequestException("jobIds must be provided");
            }

            var action = ParseAction(request.Action);
            var results = _transcodeService.ResolveJobs(request.JobIds, action);

            return TypedResults.Ok(new TranscodeBulkResolveResource
            {
                Resolved = results.Where(result => result.Success).Select(result => result.Job.ToResource()).ToList(),
                Failed = results.Where(result => !result.Success)
                                .Select(result => new TranscodeBulkResolveFailureResource { JobId = result.JobId, Error = result.Error })
                                .ToList()
            });
        }

        private TranscodeJob BuildJob(MediaType mediaType, MediaFileBase file, string path, TranscodeJobRequestResource request, TranscodeProfile profile, int? seriesId, int? movieId, TranscodeJob requeueSource = null)
        {
            // A requeue retries the original job with the exact same settings (profile, mode, codec,
            // targets, preset, device, tag, audio preference) against the current file/state.
            if (requeueSource != null)
            {
                return new TranscodeJob
                {
                    MediaType = mediaType,
                    EpisodeFileId = mediaType == MediaType.Series ? file.Id : null,
                    MovieFileId = mediaType == MediaType.Movie ? file.Id : null,
                    SeriesId = seriesId,
                    MovieId = movieId,
                    SourcePath = path,
                    SourceSize = file.Size,
                    Status = TranscodeJobStatus.Queued,
                    Mode = requeueSource.Mode,
                    VideoCodec = requeueSource.VideoCodec,
                    RateControl = requeueSource.RateControl,
                    TargetSize = requeueSource.TargetSize,
                    TargetPercent = requeueSource.TargetPercent,
                    QualityValue = requeueSource.QualityValue,
                    MaxHeight = requeueSource.MaxHeight,
                    Tag = requeueSource.Tag,
                    PreferEnglishAudio = requeueSource.PreferEnglishAudio,
                    Preset = requeueSource.Preset,
                    DeviceId = requeueSource.DeviceId,
                    ProfileId = requeueSource.ProfileId,
                    Container = requeueSource.Container,
                    Priority = requeueSource.Priority,
                    Trigger = CommandTrigger.Manual
                };
            }

            var codec = request.Codec.IsNotNullOrWhiteSpace()
                ? TranscodeCodecs.Name(TranscodeCodecs.Parse(request.Codec))
                : profile?.Codec ?? _configService.DefaultVideoCodec;

            var mode = request.Mode.IsNotNullOrWhiteSpace()
                ? ParseMode(request.Mode)
                : profile?.Mode ?? _configService.DefaultRateControlMode;

            // Clamp the one-off request values the same way the profile path does, so an out-of-range
            // value can neither make ProjectedSavings exceed the source nor emit an invalid CRF.
            var targetSize = (request.TargetSize is > 0 ? request.TargetSize.Value : (long?)null)
                ?? (profile?.TargetSizeMB is > 0 ? profile.TargetSizeMB.Value * 1024L * 1024L : (long?)null)
                ?? DefaultTargetBytes(mediaType);

            var targetPercent = mode == TranscodeMode.PercentageReduction
                ? Math.Clamp(request.TargetPercent ?? profile?.TargetPercent ?? _configService.DefaultReducePercent, 1, 99)
                : (int?)null;

            var quality = Math.Clamp(
                request.Quality ?? profile?.QualityValue ?? _configService.DefaultQualityValue,
                0,
                TranscodeArgumentBuilder.MaxQualityForCodec(TranscodeCodecs.Parse(codec)));

            var container = mode == TranscodeMode.Remux
                ? TranscodeInput.NormalizeContainer(request.Container.IsNotNullOrWhiteSpace() ? request.Container : profile?.Container)
                : null;

            return new TranscodeJob
            {
                MediaType = mediaType,
                EpisodeFileId = mediaType == MediaType.Series ? file.Id : null,
                MovieFileId = mediaType == MediaType.Movie ? file.Id : null,
                SeriesId = seriesId,
                MovieId = movieId,
                SourcePath = path,
                SourceSize = file.Size,
                Status = TranscodeJobStatus.Queued,
                Mode = mode,
                VideoCodec = codec?.ToLowerInvariant(),
                RateControl = RateControlFor(mode),
                TargetSize = mode == TranscodeMode.TargetSize ? targetSize : null,
                TargetPercent = targetPercent,
                QualityValue = quality,
                MaxHeight = mode == TranscodeMode.Remux
                    ? null
                    : TranscodeInput.NormalizeMaxHeight(request.MaxHeight is > 0 ? request.MaxHeight : profile?.MaxHeight),
                Tag = profile?.Tag,
                PreferEnglishAudio = profile?.PreferEnglishAudio ?? false,
                Preset = TranscodeInput.NormalizePreset(request.Preset) ?? profile?.Preset ?? _configService.DefaultPreset,
                DeviceId = request.DeviceId.IsNotNullOrWhiteSpace() ? request.DeviceId : profile?.DeviceId,
                ProfileId = request.ProfileId,
                Container = container,
                Priority = request.Priority,
                Trigger = CommandTrigger.Manual
            };
        }

        // A device-independent label describing how the mode controls the bitrate, surfaced in the
        // job resource and the diagnostics dump. Quality is rate-control-mode neutral because
        // software uses CRF while NVENC/VAAPI use CQ/QP. Remux copies streams so it has none.
        private static string RateControlFor(TranscodeMode mode)
        {
            return mode switch
            {
                TranscodeMode.Quality => "constant-quality",
                TranscodeMode.TargetSize => "vbr",
                TranscodeMode.PercentageReduction => "vbr",
                _ => null
            };
        }

        private long DefaultTargetBytes(MediaType mediaType)
        {
            var megabytes = mediaType == MediaType.Movie
                ? _configService.DefaultTargetMovieSizeMB
                : _configService.DefaultTargetEpisodeSizeMB;

            return (long)megabytes * 1024 * 1024;
        }

        private static TranscodeMode ParseMode(string mode)
        {
            if (!TranscodeProfileResourceMapper.TryParseMode(mode, out var parsed))
            {
                throw new BadRequestException($"Unknown transcode mode '{mode}'");
            }

            return parsed;
        }

        private TranscodeReviewAction ParseAction(string action)
        {
            if (action.IsNullOrWhiteSpace())
            {
                return _configService.TranscodeReviewDefault;
            }

            return action.Trim().ToLowerInvariant() switch
            {
                "overwrite" => TranscodeReviewAction.Overwrite,
                "keepboth" => TranscodeReviewAction.KeepBoth,
                "discard" => TranscodeReviewAction.Discard,
                _ => throw new BadRequestException($"Unknown transcode review action '{action}'")
            };
        }

        private void ValidateDevices(List<TranscodeDeviceResource> devices)
        {
            // A PUT is a full-replacement update of the known devices; an id the probe never
            // detected (or a blank one) is a client error, not a silent no-op.
            var known = _gpuCapabilityService.GetDevices() ?? new List<TranscodeDevice>();
            var knownIds = new HashSet<string>(known.Select(device => device.Id));

            foreach (var device in devices)
            {
                if (device.DeviceId.IsNullOrWhiteSpace() || !knownIds.Contains(device.DeviceId))
                {
                    throw new BadRequestException($"Transcode device '{device.DeviceId}' was not found");
                }

                // The per-device extra ffmpeg arguments are admin-supplied but land verbatim in the
                // argv string; a quote or newline could re-balance the surrounding quoting, so it is
                // rejected at the trust boundary (the builder refuses it again defensively).
                ValidateExtraArgs(device.Options?.Nvidia?.ExtraArgs, device.DeviceId);
                ValidateExtraArgs(device.Options?.Vaapi?.ExtraArgs, device.DeviceId);
            }
        }

        private static void ValidateExtraArgs(string extraArgs, string deviceId)
        {
            if (!TranscodeInput.IsValidExtraArgs(extraArgs))
            {
                throw new BadRequestException($"Extra arguments for transcode device '{deviceId}' contain a quote or line break, which is not allowed");
            }
        }

        private void ValidateProfile(TranscodeProfileResource resource, int? id)
        {
            if (resource.Name.IsNullOrWhiteSpace())
            {
                throw new BadRequestException("A profile name must be provided");
            }

            // A blank mode is "use the default" (Quality); only a non-blank unknown value is rejected
            // rather than silently coerced.
            if (resource.Mode.IsNotNullOrWhiteSpace() && !TranscodeProfileResourceMapper.TryParseMode(resource.Mode, out _))
            {
                throw new BadRequestException($"Unknown transcode mode '{resource.Mode}'");
            }

            var name = resource.Name.Trim();
            var duplicate = (_transcodeProfileService.GetAll() ?? new List<TranscodeProfile>())
                .Any(profile => profile.Id != (id ?? 0) && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                throw new BadRequestException($"A transcode profile named '{name}' already exists");
            }
        }

        private static void ValidateContainer(string container)
        {
            if (container.IsNullOrWhiteSpace())
            {
                return;
            }

            if (!TranscodeInput.IsSupportedContainer(container))
            {
                throw new BadRequestException($"Container '{container}' is not supported; expected one of: {string.Join(", ", TranscodeInput.SupportedContainers)}");
            }
        }

        private static void ValidatePreset(string preset)
        {
            // A blank preset is "unset"; only a non-blank malformed value is rejected.
            if (!TranscodeInput.IsValidPreset(preset))
            {
                throw new BadRequestException("preset is invalid");
            }
        }

        private static string NormalizeSourcePath(string path)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }
    }
}
