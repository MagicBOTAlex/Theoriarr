using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Config
{
    // D6 — dual-domain naming config. The series fields keep their untouched validation;
    // the movie fields (RenameMovies/StandardMovieFormat/MovieFolderFormat) are exposed for the
    // movies key, so a Radarr client can read/write them. Rules are scoped to the requesting
    // subsystem so a Sonarr PUT (episode-only payload) and a Radarr PUT (movie-only payload)
    // both validate.
    [V3ApiController("config/naming")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class NamingConfigController : RestController<NamingConfigResource>
    {
        private readonly INamingConfigService _namingConfigService;
        private readonly IFilenameSampleService _filenameSampleService;
        private readonly IFilenameValidationService _filenameValidationService;
        private readonly IBuildFileNames _filenameBuilder;
        private readonly bool _isMovies;

        public NamingConfigController(INamingConfigService namingConfigService,
                                  IFilenameSampleService filenameSampleService,
                                  IFilenameValidationService filenameValidationService,
                                  IBuildFileNames filenameBuilder,
                                  ISubsystemAccessor subsystemAccessor)
        {
            _namingConfigService = namingConfigService;
            _filenameSampleService = filenameSampleService;
            _filenameValidationService = filenameValidationService;
            _filenameBuilder = filenameBuilder;
            _isMovies = subsystemAccessor.Subsystem == AppSubsystem.Movies;

            if (_isMovies)
            {
                SharedValidator.RuleFor(c => c.StandardMovieFormat).ValidMovieFormat();
                SharedValidator.RuleFor(c => c.MovieFolderFormat).ValidMovieFolderFormat();
            }
            else
            {
                SharedValidator.RuleFor(c => c.MultiEpisodeStyle).InclusiveBetween(0, 5);
                SharedValidator.RuleFor(c => c.StandardEpisodeFormat).ValidEpisodeFormat();
                SharedValidator.RuleFor(c => c.DailyEpisodeFormat).ValidDailyEpisodeFormat();
                SharedValidator.RuleFor(c => c.AnimeEpisodeFormat).ValidAnimeEpisodeFormat();
                SharedValidator.RuleFor(c => c.SeriesFolderFormat).ValidSeriesFolderFormat();
                SharedValidator.RuleFor(c => c.SeasonFolderFormat).ValidSeasonFolderFormat();
                SharedValidator.RuleFor(c => c.SpecialsFolderFormat).ValidSpecialsFolderFormat();
                SharedValidator.RuleFor(c => c.CustomColonReplacementFormat).ValidCustomColonReplacement().When(c => c.ColonReplacementFormat == (int)ColonReplacementFormat.Custom);
            }
        }

        protected override NamingConfigResource GetResourceById(int id)
        {
            return GetNamingConfig();
        }

        [HttpGet]
        public NamingConfigResource GetNamingConfig()
        {
            var nameSpec = _namingConfigService.GetConfig();
            var resource = nameSpec.ToResource();

            return resource;
        }

        [RestPutById]
        public ActionResult<NamingConfigResource> UpdateNamingConfig([FromBody] NamingConfigResource resource)
        {
            // Merge onto the stored config instead of replacing it. The resource is dual-tagged
            // and a partial payload from one domain (e.g. a Radarr client sending only the movie
            // formats) must not wipe the other domain's formats by defaulting them to null/false.
            var nameSpec = resource.ToModel();

            MergeOntoStoredConfig(nameSpec);

            ValidateFormatResult(nameSpec);

            _namingConfigService.Save(nameSpec);

            return Accepted(resource.Id);
        }

        private void MergeOntoStoredConfig(NamingConfig nameSpec)
        {
            var stored = _namingConfigService.GetConfig();

            nameSpec.Id = stored.Id;

            // The shared, host-level formatting options (ReplaceIllegalCharacters, colon
            // replacement) are always taken from the payload. Only the formats belonging to
            // the other domain are preserved from the stored row, so a partial payload from
            // one domain cannot clobber the other domain's naming formats.
            if (_isMovies)
            {
                nameSpec.RenameEpisodes = stored.RenameEpisodes;
                nameSpec.MultiEpisodeStyle = stored.MultiEpisodeStyle;
                nameSpec.StandardEpisodeFormat = stored.StandardEpisodeFormat;
                nameSpec.DailyEpisodeFormat = stored.DailyEpisodeFormat;
                nameSpec.AnimeEpisodeFormat = stored.AnimeEpisodeFormat;
                nameSpec.SeriesFolderFormat = stored.SeriesFolderFormat;
                nameSpec.SeasonFolderFormat = stored.SeasonFolderFormat;
                nameSpec.SpecialsFolderFormat = stored.SpecialsFolderFormat;
            }
            else
            {
                nameSpec.RenameMovies = stored.RenameMovies;
                nameSpec.StandardMovieFormat = stored.StandardMovieFormat;
                nameSpec.MovieFolderFormat = stored.MovieFolderFormat;
            }
        }

        [HttpGet("examples")]
        public object GetExamples([FromQuery]NamingConfigResource settings)
        {
            if (settings.Id == 0)
            {
                settings = GetNamingConfig();
            }

            var nameSpec = settings.ToModel();
            var sampleResource = new NamingExampleResource();

            var singleEpisodeSampleResult = _filenameSampleService.GetStandardSample(nameSpec);
            var multiEpisodeSampleResult = _filenameSampleService.GetMultiEpisodeSample(nameSpec);
            var dailyEpisodeSampleResult = _filenameSampleService.GetDailySample(nameSpec);
            var animeEpisodeSampleResult = _filenameSampleService.GetAnimeSample(nameSpec);
            var animeMultiEpisodeSampleResult = _filenameSampleService.GetAnimeMultiEpisodeSample(nameSpec);

            sampleResource.SingleEpisodeExample = _filenameValidationService.ValidateStandardFilename(singleEpisodeSampleResult) != null
                    ? null
                    : singleEpisodeSampleResult.FileName;

            sampleResource.MultiEpisodeExample = _filenameValidationService.ValidateStandardFilename(multiEpisodeSampleResult) != null
                    ? null
                    : multiEpisodeSampleResult.FileName;

            sampleResource.DailyEpisodeExample = _filenameValidationService.ValidateDailyFilename(dailyEpisodeSampleResult) != null
                    ? null
                    : dailyEpisodeSampleResult.FileName;

            sampleResource.AnimeEpisodeExample = _filenameValidationService.ValidateAnimeFilename(animeEpisodeSampleResult) != null
                    ? null
                    : animeEpisodeSampleResult.FileName;

            sampleResource.AnimeMultiEpisodeExample = _filenameValidationService.ValidateAnimeFilename(animeMultiEpisodeSampleResult) != null
                    ? null
                    : animeMultiEpisodeSampleResult.FileName;

            sampleResource.SeriesFolderExample = nameSpec.SeriesFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSeriesFolderSample(nameSpec);

            sampleResource.SeasonFolderExample = nameSpec.SeasonFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSeasonFolderSample(nameSpec);

            sampleResource.SpecialsFolderExample = nameSpec.SpecialsFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSpecialsFolderSample(nameSpec);

            var movieSampleResult = _filenameSampleService.GetMovieSample(nameSpec);

            sampleResource.MovieExample = nameSpec.StandardMovieFormat.IsNullOrWhiteSpace()
                ? null
                : movieSampleResult.FileName;

            sampleResource.MovieFolderExample = nameSpec.MovieFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetMovieFolderSample(nameSpec);

            return sampleResource;
        }

        private void ValidateFormatResult(NamingConfig nameSpec)
        {
            var validationFailures = new List<ValidationFailure>();

            if (_isMovies)
            {
                var movieSampleResult = _filenameSampleService.GetMovieSample(nameSpec);
                validationFailures.AddIfNotNull(_filenameValidationService.ValidateMovieFilename(movieSampleResult));
            }
            else
            {
                var singleEpisodeSampleResult = _filenameSampleService.GetStandardSample(nameSpec);
                var multiEpisodeSampleResult = _filenameSampleService.GetMultiEpisodeSample(nameSpec);
                var dailyEpisodeSampleResult = _filenameSampleService.GetDailySample(nameSpec);
                var animeEpisodeSampleResult = _filenameSampleService.GetAnimeSample(nameSpec);
                var animeMultiEpisodeSampleResult = _filenameSampleService.GetAnimeMultiEpisodeSample(nameSpec);

                validationFailures.AddIfNotNull(_filenameValidationService.ValidateStandardFilename(singleEpisodeSampleResult));
                validationFailures.AddIfNotNull(_filenameValidationService.ValidateStandardFilename(multiEpisodeSampleResult));
                validationFailures.AddIfNotNull(_filenameValidationService.ValidateDailyFilename(dailyEpisodeSampleResult));
                validationFailures.AddIfNotNull(_filenameValidationService.ValidateAnimeFilename(animeEpisodeSampleResult));
                validationFailures.AddIfNotNull(_filenameValidationService.ValidateAnimeFilename(animeMultiEpisodeSampleResult));
            }

            if (validationFailures.Any())
            {
                throw new ValidationException(validationFailures.DistinctBy(v => v.PropertyName).ToArray());
            }
        }
    }
}
