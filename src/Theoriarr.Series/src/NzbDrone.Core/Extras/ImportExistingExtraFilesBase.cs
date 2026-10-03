using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras
{
    public abstract class ImportExistingExtraFilesBase<TExtraFile> : IImportExistingExtraFiles
        where TExtraFile : ExtraFile, new()
    {
        private readonly IExtraFileService<TExtraFile> _extraFileService;

        public ImportExistingExtraFilesBase(IExtraFileService<TExtraFile> extraFileService)
        {
            _extraFileService = extraFileService;
        }

        public abstract int Order { get; }
        public abstract IEnumerable<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename);
        public abstract IEnumerable<ExtraFile> ProcessFiles(Movie movie, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename);

        public virtual ImportExistingExtraFileFilterResult<TExtraFile> FilterAndClean(Series series, List<string> filesOnDisk, List<string> importedFiles, bool keepExistingEntries)
        {
            var seriesFiles = _extraFileService.GetFilesBySeries(series.Id);

            if (keepExistingEntries)
            {
                var incompleteImports = seriesFiles.IntersectBy(f => Path.Combine(series.Path, f.RelativePath), filesOnDisk, i => i, PathEqualityComparer.Instance).Select(f => f.Id);

                _extraFileService.DeleteMany(incompleteImports);

                return Filter(series.Path, filesOnDisk, importedFiles, new List<TExtraFile>());
            }

            Clean(series.Path, filesOnDisk, importedFiles, seriesFiles);

            return Filter(series.Path, filesOnDisk, importedFiles, seriesFiles);
        }

        public virtual ImportExistingExtraFileFilterResult<TExtraFile> FilterAndClean(Movie movie, List<string> filesOnDisk, List<string> importedFiles, bool keepExistingEntries)
        {
            var movieFiles = _extraFileService.GetFilesByMovie(movie.Id);

            if (keepExistingEntries)
            {
                var incompleteImports = movieFiles.IntersectBy(f => Path.Combine(movie.Path, f.RelativePath), filesOnDisk, i => i, PathEqualityComparer.Instance).Select(f => f.Id);

                _extraFileService.DeleteMany(incompleteImports);

                return Filter(movie.Path, filesOnDisk, importedFiles, new List<TExtraFile>());
            }

            Clean(movie.Path, filesOnDisk, importedFiles, movieFiles);

            return Filter(movie.Path, filesOnDisk, importedFiles, movieFiles);
        }

        private ImportExistingExtraFileFilterResult<TExtraFile> Filter(string mediaPath, List<string> filesOnDisk, List<string> importedFiles, List<TExtraFile> extraFiles)
        {
            var previouslyImported = extraFiles.IntersectBy(s => Path.Combine(mediaPath, s.RelativePath), filesOnDisk, f => f, PathEqualityComparer.Instance).ToList();
            var filteredFiles = filesOnDisk.Except(previouslyImported.Select(f => Path.Combine(mediaPath, f.RelativePath)).ToList(), PathEqualityComparer.Instance)
                                           .Except(importedFiles, PathEqualityComparer.Instance)
                                           .ToList();

            // Return files that are already imported so they aren't imported again by other importers.
            // Filter out files that were previously imported and as well as ones imported by other importers.
            return new ImportExistingExtraFileFilterResult<TExtraFile>(previouslyImported, filteredFiles);
        }

        private void Clean(string mediaPath, List<string> filesOnDisk, List<string> importedFiles, List<TExtraFile> extraFiles)
        {
            var alreadyImportedFileIds = extraFiles.IntersectBy(f => Path.Combine(mediaPath, f.RelativePath), importedFiles, i => i, PathEqualityComparer.Instance)
                .Select(f => f.Id);

            var deletedFiles = extraFiles.ExceptBy(f => Path.Combine(mediaPath, f.RelativePath), filesOnDisk, i => i, PathEqualityComparer.Instance)
                .Select(f => f.Id);

            _extraFileService.DeleteMany(alreadyImportedFileIds);
            _extraFileService.DeleteMany(deletedFiles);
        }
    }
}
