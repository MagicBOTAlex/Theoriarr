using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.ImportLists.Exclusions
{
    // D6 — one service over the merged, MediaType-tagged exclusion store. It handles the
    // deletion events for both domains (series deletions add a TvdbId row; movie deletions add a
    // TmdbId row) and exposes the movie vocabulary helpers the discovery/collection code uses.
    public interface IImportListExclusionService
    {
        ImportListExclusion Add(ImportListExclusion importListExclusion);
        List<ImportListExclusion> Add(List<ImportListExclusion> importListExclusions);
        List<ImportListExclusion> All();
        PagingSpec<ImportListExclusion> Paged(PagingSpec<ImportListExclusion> pagingSpec);
        bool IsMovieExcluded(int tmdbId);
        void Delete(int id);
        void Delete(List<int> ids);
        ImportListExclusion Get(int id);
        ImportListExclusion FindByTvdbId(int tvdbId);
        ImportListExclusion Update(ImportListExclusion importListExclusion);
        List<int> AllExcludedTmdbIds();
    }

    public class ImportListExclusionService : IImportListExclusionService,
                                              IHandleAsync<SeriesDeletedEvent>,
                                              IHandleAsync<MoviesDeletedEvent>
    {
        private readonly IImportListExclusionRepository _repo;
        private readonly Logger _logger;

        public ImportListExclusionService(IImportListExclusionRepository repo, Logger logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public ImportListExclusion Add(ImportListExclusion importListExclusion)
        {
            if (importListExclusion.MediaType == MediaType.Movie &&
                _repo.IsMovieExcluded(importListExclusion.TmdbId))
            {
                return _repo.FindByTmdbid(importListExclusion.TmdbId);
            }

            return _repo.Insert(importListExclusion);
        }

        public List<ImportListExclusion> Add(List<ImportListExclusion> importListExclusions)
        {
            var seriesExclusions = importListExclusions.Where(x => x.MediaType != MediaType.Movie).ToList();
            var movieExclusions = importListExclusions.Where(x => x.MediaType == MediaType.Movie).ToList();

            if (seriesExclusions.Any())
            {
                _repo.InsertMany(seriesExclusions);
            }

            if (movieExclusions.Any())
            {
                _repo.InsertMany(DeDupeMovieExclusions(movieExclusions));
            }

            return importListExclusions;
        }

        public ImportListExclusion Update(ImportListExclusion importListExclusion)
        {
            return _repo.Update(importListExclusion);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public void Delete(List<int> ids)
        {
            _repo.DeleteMany(ids);
        }

        public ImportListExclusion Get(int id)
        {
            return _repo.Get(id);
        }

        public ImportListExclusion FindByTvdbId(int tvdbId)
        {
            return _repo.FindByTvdbId(tvdbId);
        }

        public bool IsMovieExcluded(int tmdbId)
        {
            return _repo.IsMovieExcluded(tmdbId);
        }

        public List<ImportListExclusion> All()
        {
            return _repo.All().ToList();
        }

        public PagingSpec<ImportListExclusion> Paged(PagingSpec<ImportListExclusion> pagingSpec)
        {
            return _repo.GetPaged(pagingSpec);
        }

        public List<int> AllExcludedTmdbIds()
        {
            return _repo.AllExcludedTmdbIds();
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            if (!message.AddImportListExclusion)
            {
                return;
            }

            var exclusionsToAdd = new List<ImportListExclusion>();

            foreach (var series in message.Series.DistinctBy(s => s.TvdbId))
            {
                var existingExclusion = _repo.FindByTvdbId(series.TvdbId);

                if (existingExclusion != null)
                {
                    continue;
                }

                exclusionsToAdd.Add(new ImportListExclusion
                {
                    MediaType = MediaType.Series,
                    TvdbId = series.TvdbId,
                    Title = series.Title
                });
            }

            _repo.InsertMany(exclusionsToAdd);
        }

        public void HandleAsync(MoviesDeletedEvent message)
        {
            if (!message.AddImportListExclusion)
            {
                return;
            }

            _logger.Debug("Adding {0} deleted movies to import list exclusions.", message.Movies.Count);

            var exclusionsToAdd = DeDupeMovieExclusions(message.Movies.Select(m => new ImportListExclusion
            {
                MediaType = MediaType.Movie,
                TmdbId = m.TmdbId,
                Title = m.Title,
                MovieYear = m.Year
            }).ToList());

            _repo.InsertMany(exclusionsToAdd);
        }

        private List<ImportListExclusion> DeDupeMovieExclusions(List<ImportListExclusion> exclusions)
        {
            var existingExclusions = _repo.AllExcludedTmdbIds();

            return exclusions
                .DistinctBy(x => x.TmdbId)
                .Where(x => !existingExclusions.Contains(x.TmdbId))
                .ToList();
        }
    }
}
