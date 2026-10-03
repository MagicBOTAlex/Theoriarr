using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.TPL;
using NzbDrone.Core.ImportLists.ImportListItems;
using NzbDrone.Core.ImportLists.ImportListMovies;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.ImportLists
{
    public interface IFetchAndParseImportList
    {
        ImportListFetchResult Fetch();
        ImportListFetchResult FetchSingleList(ImportListDefinition definition);
    }

    // D5: single skeleton, two item pipelines selected by IImportList.MediaType.
    public class FetchAndParseImportListService : IFetchAndParseImportList
    {
        private readonly IImportListFactory _importListFactory;
        private readonly IImportListStatusService _importListStatusService;
        private readonly IImportListItemService _importListItemService;
        private readonly IImportListMovieService _listMovieService;
        private readonly ISearchForNewMovie _movieSearch;
        private readonly IProvideMovieInfo _movieInfoService;
        private readonly IMovieMetadataService _movieMetadataService;
        private readonly Logger _logger;

        public FetchAndParseImportListService(IImportListFactory importListFactory,
                                              IImportListStatusService importListStatusService,
                                              IImportListItemService importListItemService,
                                              IImportListMovieService listMovieService,
                                              ISearchForNewMovie movieSearch,
                                              IProvideMovieInfo movieInfoService,
                                              IMovieMetadataService movieMetadataService,
                                              Logger logger)
        {
            _importListFactory = importListFactory;
            _importListStatusService = importListStatusService;
            _importListItemService = importListItemService;
            _listMovieService = listMovieService;
            _movieSearch = movieSearch;
            _movieInfoService = movieInfoService;
            _movieMetadataService = movieMetadataService;
            _logger = logger;
        }

        public ImportListFetchResult Fetch()
        {
            var result = new ImportListFetchResult();

            var importLists = _importListFactory.AutomaticAddEnabled();

            if (!importLists.Any())
            {
                _logger.Debug("No enabled import lists, skipping.");
                return result;
            }

            _logger.Debug("Available import lists {0}", importLists.Count);

            var taskList = new List<Task>();
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            var blockedLists = _importListStatusService.GetBlockedProviders().ToDictionary(v => v.ProviderId, v => v);

            foreach (var importList in importLists)
            {
                var importListLocal = importList;
                var importListStatus = _importListStatusService.GetListStatus(importListLocal.Definition.Id).LastInfoSync;

                if (importListStatus.HasValue)
                {
                    var importListNextSync = importListStatus.Value + importListLocal.MinRefreshInterval;

                    if (DateTime.UtcNow < importListNextSync)
                    {
                        _logger.Trace("Skipping refresh of Import List {0} ({1}) due to minimum refresh interval. Next sync after {2}", importList.Name, importListLocal.Definition.Name, importListNextSync);
                        continue;
                    }
                }

                if (blockedLists.TryGetValue(importList.Definition.Id, out var blockedListStatus))
                {
                    _logger.Debug("Temporarily ignoring Import List {0} ({1}) till {2} due to recent failures.", importList.Name, importListLocal.Definition.Name, blockedListStatus.DisabledTill.Value.ToLocalTime());
                    result.AnyFailure |= true; // Ensure we don't clean if a list is down
                    continue;
                }

                var task = taskFactory.StartNew(() =>
                {
                    try
                    {
                        var fetchResult = importListLocal.Fetch();

                        lock (result)
                        {
                            if (importListLocal.MediaType == MediaType.Movie)
                            {
                                FetchMovies(result, importList, fetchResult);
                            }
                            else
                            {
                                FetchSeries(result, importList, fetchResult);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Error during Import List Sync of {0} ({1})", importList.Name, importListLocal.Definition.Name);
                    }
                }).LogExceptions();

                taskList.Add(task);
            }

            Task.WaitAll(taskList.ToArray());

            result.Series = result.Series.DistinctBy(r => new { r.TvdbId, r.ImdbId, r.Title }).ToList();
            result.Movies = result.Movies.DistinctBy(r => new { r.TmdbId, r.ImdbId, r.Title }).ToList();

            _logger.Debug("Found {0} series and {1} movie reports from {2} lists", result.Series.Count, result.Movies.Count, importLists.Count);

            return result;
        }

        public ImportListFetchResult FetchSingleList(ImportListDefinition definition)
        {
            var result = new ImportListFetchResult();

            var importList = _importListFactory.GetInstance(definition);

            if (importList == null || !definition.Enabled)
            {
                _logger.Debug("Import List {0} ({1}) is not enabled, skipping.", importList.Name, importList.Definition.Name);
                return result;
            }

            try
            {
                var fetchResult = importList.Fetch();

                if (importList.MediaType == MediaType.Movie)
                {
                    FetchMovies(result, importList, fetchResult);
                }
                else
                {
                    FetchSeries(result, importList, fetchResult);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error during Import List Sync of {0} ({1})", importList.Name, importList.Definition.Name);
            }

            result.Series = result.Series.DistinctBy(r => new { r.TvdbId, r.ImdbId, r.Title }).ToList();
            result.Movies = result.Movies.DistinctBy(r => new { r.TmdbId, r.ImdbId, r.Title }).ToList();

            return result;
        }

        private void FetchSeries(ImportListFetchResult result, IImportList importList, ImportListFetchResult fetchResult)
        {
            var importListReports = fetchResult.Series;

            _logger.Debug("Found {0} reports from {1} ({2})", importListReports.Count, importList.Name, importList.Definition.Name);

            if (!fetchResult.AnyFailure)
            {
                importListReports.ForEach(s => s.ImportListId = importList.Definition.Id);
                result.Series.AddRange(importListReports);
                var removed = _importListItemService.SyncSeriesForList(importListReports, importList.Definition.Id);
                _importListStatusService.UpdateListSyncStatus(importList.Definition.Id, removed > 0);
            }

            result.AnyFailure |= fetchResult.AnyFailure;
        }

        private void FetchMovies(ImportListFetchResult result, IImportList importList, ImportListFetchResult fetchResult)
        {
            _logger.Debug("Found {0} from Import List {1} ({2})", fetchResult.Movies.Count, importList.Name, importList.Definition.Name);

            if (!fetchResult.AnyFailure)
            {
                var alreadyMapped = result.Movies.Where(x => fetchResult.Movies.Any(r => r.TmdbId == x.TmdbId));
                var listMovies = MapMovieReports(fetchResult.Movies.Where(x => result.Movies.All(r => r.TmdbId != x.TmdbId))).Where(x => x.TmdbId > 0).ToList();

                listMovies.AddRange(alreadyMapped);
                listMovies = listMovies.DistinctBy(x => x.TmdbId).ToList();
                listMovies.ForEach(m => m.ListId = importList.Definition.Id);

                result.Movies.AddRange(listMovies);
                _listMovieService.SyncMoviesForList(listMovies, importList.Definition.Id);
            }

            result.AnyFailure |= fetchResult.AnyFailure;
            result.SyncedLists++;

            _importListStatusService.UpdateListSyncStatus(importList.Definition.Id);
        }

        private List<ImportListMovie> MapMovieReports(IEnumerable<ImportListMovie> reports)
        {
            var mappedMovies = reports.Select(m => _movieSearch.MapMovieToTmdbMovie(new MovieMetadata { Title = m.Title, TmdbId = m.TmdbId, ImdbId = m.ImdbId, Year = m.Year }))
                .Where(x => x != null)
                .DistinctBy(x => x.TmdbId)
                .ToList();

            _movieMetadataService.UpsertMany(mappedMovies);

            var mappedListMovies = new List<ImportListMovie>();

            foreach (var movieMeta in mappedMovies)
            {
                var mappedListMovie = new ImportListMovie();

                if (movieMeta != null)
                {
                    mappedListMovie.MovieMetadata = movieMeta;
                    mappedListMovie.MovieMetadataId = movieMeta.Id;
                }

                mappedListMovies.Add(mappedListMovie);
            }

            return mappedListMovies;
        }
    }
}
