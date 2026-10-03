using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using MovieAggregationService = NzbDrone.Core.MediaFiles.MovieImport.Aggregation.IAggregationService;
using MovieAugmentingFailedException = NzbDrone.Core.MediaFiles.MovieImport.Aggregation.AugmentingFailedException;
using SeriesAggregationService = NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.IAggregationService;
using SeriesAugmentingFailedException = NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.AugmentingFailedException;

namespace NzbDrone.Core.Extras.Subtitles
{
    public class ExistingSubtitleImporter : ImportExistingExtraFilesBase<SubtitleFile>
    {
        private readonly IExtraFileService<SubtitleFile> _subtitleFileService;
        private readonly SeriesAggregationService _aggregationService;
        private readonly MovieAggregationService _movieAggregationService;
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public ExistingSubtitleImporter(IExtraFileService<SubtitleFile> subtitleFileService,
                                        SeriesAggregationService aggregationService,
                                        MovieAggregationService movieAggregationService,
                                        IParsingService parsingService,
                                        Logger logger)
            : base(subtitleFileService)
        {
            _subtitleFileService = subtitleFileService;
            _aggregationService = aggregationService;
            _movieAggregationService = movieAggregationService;
            _parsingService = parsingService;
            _logger = logger;
        }

        public override int Order => 1;

        public override IEnumerable<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename)
        {
            _logger.Debug("Looking for existing subtitle files in {0}", series.Path);

            var subtitleFiles = new List<SubtitleFile>();
            var filterResult = FilterAndClean(series, filesOnDisk, importedFiles, fileNameBeforeRename is not null);

            foreach (var possibleSubtitleFile in filterResult.FilesOnDisk)
            {
                var extension = Path.GetExtension(possibleSubtitleFile);

                if (SubtitleFileExtensions.Extensions.Contains(extension))
                {
                    var localEpisode = new LocalEpisode
                    {
                        FileEpisodeInfo = Parser.Parser.ParsePath(possibleSubtitleFile),
                        Series = series,
                        Path = possibleSubtitleFile,
                        FileNameBeforeRename = fileNameBeforeRename
                    };

                    try
                    {
                        _aggregationService.Augment(localEpisode, null);
                    }
                    catch (SeriesAugmentingFailedException)
                    {
                        _logger.Debug("Unable to parse extra file: {0}", possibleSubtitleFile);
                        continue;
                    }

                    if (localEpisode.Episodes.Empty())
                    {
                        _logger.Debug("Cannot find related episodes for: {0}", possibleSubtitleFile);
                        continue;
                    }

                    if (localEpisode.Episodes.DistinctBy(e => e.EpisodeFileId).Count() > 1)
                    {
                        _logger.Debug("Subtitle file: {0} does not match existing files.", possibleSubtitleFile);
                        continue;
                    }

                    var firstEpisode = localEpisode.Episodes.First();

                    var subtitleFile = new SubtitleFile
                                       {
                                           SeriesId = series.Id,
                                           SeasonNumber = localEpisode.SeasonNumber,
                                           EpisodeFileId = firstEpisode.EpisodeFileId,
                                           MediaType = Files.MediaType.Series,
                                           RelativePath = series.Path.GetRelativePath(possibleSubtitleFile),
                                           Language = localEpisode.SubtitleInfo?.Language ?? Language.Unknown,
                                           LanguageTags = localEpisode.SubtitleInfo?.LanguageTags ?? new List<string>(),
                                           Title = localEpisode.SubtitleInfo?.Title,
                                           Extension = extension,
                                           Copy = localEpisode.SubtitleInfo?.Copy ?? 0
                                       };

                    subtitleFiles.Add(subtitleFile);
                }
            }

            _logger.Info("Found {0} existing subtitle files", subtitleFiles.Count);
            _subtitleFileService.Upsert(subtitleFiles);

            // Return files that were just imported along with files that were
            // previously imported so previously imported files aren't imported twice

            return subtitleFiles.Concat(filterResult.PreviouslyImported);
        }

        public override IEnumerable<ExtraFile> ProcessFiles(Movie movie, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename)
        {
            _logger.Debug("Looking for existing subtitle files in {0}", movie.Path);

            var subtitleFiles = new List<SubtitleFile>();
            var filterResult = FilterAndClean(movie, filesOnDisk, importedFiles, fileNameBeforeRename is not null);

            foreach (var possibleSubtitleFile in filterResult.FilesOnDisk)
            {
                var extension = Path.GetExtension(possibleSubtitleFile);

                if (SubtitleFileExtensions.Extensions.Contains(extension))
                {
                    var minimalInfo = _parsingService.ParseMinimalPathMovieInfo(possibleSubtitleFile);

                    if (minimalInfo == null)
                    {
                        _logger.Debug("Unable to parse subtitle file: {0}", possibleSubtitleFile);
                        continue;
                    }

                    var localMovie = new LocalMovie
                    {
                        FileMovieInfo = minimalInfo,
                        Movie = movie,
                        Path = possibleSubtitleFile,
                        FileNameBeforeRename = fileNameBeforeRename
                    };

                    try
                    {
                        _movieAggregationService.Augment(localMovie, null);
                    }
                    catch (MovieAugmentingFailedException)
                    {
                        _logger.Debug("Unable to parse extra file: {0}", possibleSubtitleFile);
                        continue;
                    }

                    var subtitleFile = new SubtitleFile
                    {
                        MovieId = movie.Id,
                        MovieFileId = movie.MovieFileId,
                        MediaType = Files.MediaType.Movie,
                        RelativePath = movie.Path.GetRelativePath(possibleSubtitleFile),
                        Language = localMovie.SubtitleInfo.Language,
                        LanguageTags = localMovie.SubtitleInfo.LanguageTags,
                        Title = localMovie.SubtitleInfo.Title,
                        Extension = extension,
                        Copy = localMovie.SubtitleInfo.Copy
                    };

                    subtitleFiles.Add(subtitleFile);
                }
            }

            _logger.Info("Found {0} existing subtitle files", subtitleFiles.Count);
            _subtitleFileService.Upsert(subtitleFiles);

            // Return files that were just imported along with files that were
            // previously imported so previously imported files aren't imported twice
            return subtitleFiles.Concat(filterResult.PreviouslyImported);
        }
    }
}
