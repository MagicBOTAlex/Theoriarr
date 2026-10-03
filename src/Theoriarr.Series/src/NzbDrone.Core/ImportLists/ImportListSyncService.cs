using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists.Exclusions;
using NzbDrone.Core.ImportLists.ImportListItems;
using NzbDrone.Core.ImportLists.ImportListMovies;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.ThingiProvider.Events;
using NzbDrone.Core.Tv;
using MovieMonitorTypes = NzbDrone.Core.Movies.MonitorTypes;
using TvMonitorTypes = NzbDrone.Core.Tv.MonitorTypes;

namespace NzbDrone.Core.ImportLists
{
    // D5: one command executor, two item pipelines. Series providers feed ProcessSeriesListItems,
    // movie providers feed ProcessMovieListItems; cleaning is per-media.
    public class ImportListSyncService : IExecute<ImportListSyncCommand>, IHandleAsync<ProviderDeletedEvent<IImportList>>
    {
        private readonly IImportListFactory _importListFactory;
        private readonly IImportListStatusService _importListStatusService;
        private readonly IImportListExclusionService _importListExclusionService;
        private readonly IImportListItemService _importListItemService;
        private readonly IImportListMovieService _listMovieService;
        private readonly IFetchAndParseImportList _listFetcherAndParser;
        private readonly ISearchForNewSeries _seriesSearchService;
        private readonly ISeriesService _seriesService;
        private readonly IAddSeriesService _addSeriesService;
        private readonly IMovieService _movieService;
        private readonly IAddMovieService _addMovieService;
        private readonly IConfigService _configService;
        private readonly ITaskManager _taskManager;
        private readonly Logger _logger;

        public ImportListSyncService(IImportListFactory importListFactory,
            IImportListStatusService importListStatusService,
            IImportListExclusionService importListExclusionService,
            IImportListItemService importListItemService,
            IImportListMovieService listMovieService,
            IFetchAndParseImportList listFetcherAndParser,
            ISearchForNewSeries seriesSearchService,
            ISeriesService seriesService,
            IAddSeriesService addSeriesService,
            IMovieService movieService,
            IAddMovieService addMovieService,
            IConfigService configService,
            ITaskManager taskManager,
            Logger logger)
        {
            _importListFactory = importListFactory;
            _importListStatusService = importListStatusService;
            _importListExclusionService = importListExclusionService;
            _importListItemService = importListItemService;
            _listMovieService = listMovieService;
            _listFetcherAndParser = listFetcherAndParser;
            _seriesSearchService = seriesSearchService;
            _seriesService = seriesService;
            _addSeriesService = addSeriesService;
            _movieService = movieService;
            _addMovieService = addMovieService;
            _configService = configService;
            _taskManager = taskManager;
            _logger = logger;
        }

        private bool AllListsSuccessfulWithAPendingClean()
        {
            var lists = _importListFactory.AutomaticAddEnabled(false);
            var anyRemoved = false;

            foreach (var list in lists)
            {
                var status = _importListStatusService.GetListStatus(list.Definition.Id);

                if (status.DisabledTill.HasValue)
                {
                    // list failed the last time it was synced.
                    return false;
                }

                if (!status.LastInfoSync.HasValue)
                {
                    // list has never been synced.
                    return false;
                }

                anyRemoved |= status.HasRemovedItemSinceLastClean;
            }

            return anyRemoved;
        }

        private void SyncAll()
        {
            if (_importListFactory.AutomaticAddEnabled().Empty())
            {
                _logger.Debug("No enabled import lists, skipping sync and cleaning");

                return;
            }

            _logger.ProgressInfo("Starting Import List Sync");

            var result = _listFetcherAndParser.Fetch();

            // SERIES pipeline (auto-add gated per definition inside ProcessSeriesListItems).
            if (result.Series.Any())
            {
                ProcessSeriesListItems(result.Series.ToList());
            }

            // MOVIES pipeline: clean only when at least one list synced and nothing failed, then add.
            if (!result.AnyFailure && result.SyncedLists > 0)
            {
                CleanMovieLibrary();
            }

            if (result.Movies.Any())
            {
                ProcessMovieListItems(result);
            }

            TryCleanSeriesLibrary();
        }

        private void SyncList(ImportListDefinition definition)
        {
            _logger.ProgressInfo("Starting Import List Refresh for List {0}", definition.Name);

            var result = _listFetcherAndParser.FetchSingleList(definition);

            if (definition.MediaType == MediaType.Movie)
            {
                // Single-list refresh does not clean (matches MOVIES SyncList).
                ProcessMovieListItems(result);
            }
            else
            {
                ProcessSeriesListItems(result.Series.ToList());
                TryCleanSeriesLibrary();
            }
        }

        public void Execute(ImportListSyncCommand message)
        {
            if (message.DefinitionId.HasValue)
            {
                SyncList(_importListFactory.Get(message.DefinitionId.Value));
            }
            else
            {
                SyncAll();
            }
        }

        public void HandleAsync(ProviderDeletedEvent<IImportList> message)
        {
            TryCleanSeriesLibrary();
        }

        // ---------------------------------------------------------------- SERIES pipeline

        private void ProcessSeriesListItems(List<ImportListItemInfo> items)
        {
            var seriesToAdd = new List<Series>();

            if (items.Count == 0)
            {
                _logger.ProgressInfo("No list items to process");

                return;
            }

            _logger.ProgressInfo("Processing {0} list items", items.Count);

            var reportNumber = 1;

            var listExclusions = _importListExclusionService.All();
            var importLists = _importListFactory.All();

            var existingSeriesIds = _seriesService.AllSeriesTvdbIds();

            var existingSeriesToUpdate = new Dictionary<int, HashSet<int>>();

            foreach (var item in items)
            {
                _logger.ProgressTrace("Processing list item {0}/{1}", reportNumber, items.Count);

                reportNumber++;

                var importList = importLists.Single(x => x.Id == item.ImportListId);

                if (!importList.EnableAutomaticAdd)
                {
                    continue;
                }

                // Map by IMDb ID if we have it
                if (item.TvdbId <= 0 && item.ImdbId.IsNotNullOrWhiteSpace())
                {
                    var mappedSeries = _seriesSearchService.SearchForNewSeriesByImdbId(item.ImdbId)
                        .FirstOrDefault();

                    if (mappedSeries != null)
                    {
                        item.TvdbId = mappedSeries.TvdbId;
                        item.Title = mappedSeries?.Title;
                    }
                }

                // Map by TMDb ID if we have it
                if (item.TvdbId <= 0 && item.TmdbId > 0)
                {
                    var mappedSeries = _seriesSearchService.SearchForNewSeriesByTmdbId(item.TmdbId)
                        .FirstOrDefault();

                    if (mappedSeries != null)
                    {
                        item.TvdbId = mappedSeries.TvdbId;
                        item.Title = mappedSeries?.Title;
                    }
                }

                // Map by AniList ID if we have it
                if (item.TvdbId <= 0 && item.AniListId > 0)
                {
                    var mappedSeries = _seriesSearchService.SearchForNewSeriesByAniListId(item.AniListId)
                        .FirstOrDefault();

                    if (mappedSeries == null)
                    {
                        _logger.Debug("Rejected, unable to find matching TVDB ID for Anilist ID: {0} [{1}]", item.AniListId, item.Title);

                        continue;
                    }

                    item.TvdbId = mappedSeries.TvdbId;
                    item.Title = mappedSeries.Title;
                }

                // Map by MyAniList ID if we have it
                if (item.TvdbId <= 0 && item.MalId > 0)
                {
                    var mappedSeries = _seriesSearchService.SearchForNewSeriesByMyAnimeListId(item.MalId)
                        .FirstOrDefault();

                    if (mappedSeries == null)
                    {
                        _logger.Debug("Rejected, unable to find matching TVDB ID for MAL ID: {0} [{1}]", item.MalId, item.Title);

                        continue;
                    }

                    item.TvdbId = mappedSeries.TvdbId;
                    item.Title = mappedSeries.Title;
                }

                if (item.TvdbId == 0)
                {
                    _logger.Debug("[{0}] Rejected, unable to find TVDB ID", item.Title);
                    continue;
                }

                // Check to see if series excluded
                var excludedSeries = listExclusions.SingleOrDefault(s => s.TvdbId == item.TvdbId);

                if (excludedSeries != null)
                {
                    _logger.Debug("{0} [{1}] Rejected due to list exclusion", item.TvdbId, item.Title);
                    continue;
                }

                // Break if Series Exists in DB, if it exists, update the tags with the tags in the import list and move to the next item
                var existingSeriesId = existingSeriesIds.FirstOrDefault(x => x.Value == item.TvdbId).Key;

                if (existingSeriesId > 0)
                {
                    QueueTagsOnPendingSeries(importList, existingSeriesToUpdate, existingSeriesId);

                    _logger.Debug("{0} [{1}] Rejected, series exists in database", item.TvdbId, item.Title);
                    continue;
                }

                // search the existing seriesToAdd queue to see if we already have the series queued to insert
                var pendingSeries = seriesToAdd.FirstOrDefault(s => s.TvdbId == item.TvdbId);

                // Append Series if not already in DB or already on add list
                if (pendingSeries == null)
                {
                    var monitored = importList.ShouldMonitor != TvMonitorTypes.None;

                    seriesToAdd.Add(new Series
                    {
                        TvdbId = item.TvdbId,
                        Title = item.Title,
                        Year = item.Year,
                        Monitored = monitored,
                        MonitorNewItems = importList.MonitorNewItems,
                        RootFolderPath = importList.RootFolderPath,
                        QualityProfileId = importList.QualityProfileId,
                        SeriesType = importList.SeriesType,
                        SeasonFolder = importList.SeasonFolder,
                        Seasons = item.Seasons,
                        Tags = importList.Tags,
                        AddOptions = new AddSeriesOptions
                        {
                            SearchForMissingEpisodes = importList.SearchForMissingEpisodes,

                            // If seasons are provided use them for syncing monitored status, otherwise use the list setting.
                            Monitor = item.Seasons.Any() ? TvMonitorTypes.Skip : importList.ShouldMonitor
                        }
                    });
                }
                else
                {
                    // Add the tags for the current import list to the existing queued series.
                    foreach (var tag in importList.Tags)
                    {
                        pendingSeries.Tags.Add(tag);
                    }
                }
            }

            _addSeriesService.AddSeries(seriesToAdd, true);
            UpdateTagsOnPendingSeries(existingSeriesToUpdate);

            _logger.ProgressInfo("Import List Sync Completed. Items found: {0}, Series added: {1}", items.Count, seriesToAdd.Count);
        }

        private void QueueTagsOnPendingSeries(ImportListDefinition importList, Dictionary<int, HashSet<int>> existingSeriesToUpdate, int existingSeriesId)
        {
            if (!importList.TagExisting || importList.Tags.Count == 0)
            {
                return;
            }

            if (existingSeriesToUpdate.TryGetValue(existingSeriesId, out var tagsToAdd))
            {
                foreach (var importListTag in importList.Tags)
                {
                    tagsToAdd.Add(importListTag);
                }
            }
            else
            {
                existingSeriesToUpdate.Add(existingSeriesId, new HashSet<int>(importList.Tags));
            }
        }

        private void UpdateTagsOnPendingSeries(Dictionary<int, HashSet<int>> existingSeriesToUpdate)
        {
            if (existingSeriesToUpdate.Count == 0)
            {
                return;
            }

            var possibleSeriesToUpdate = _seriesService.GetSeries(existingSeriesToUpdate.Keys);
            var seriesWithUpdatedTags = new List<Series>();

            foreach (var series in possibleSeriesToUpdate)
            {
                var tags = existingSeriesToUpdate[series.Id];
                var currentTagsCount = series.Tags.Count;

                foreach (var tag in tags)
                {
                    series.Tags.Add(tag);
                }

                if (currentTagsCount != series.Tags.Count)
                {
                    _logger.Debug("{0} [{1}] tagged existing series", series.TvdbId, series.Title);
                    seriesWithUpdatedTags.Add(series);
                }
            }

            _seriesService.UpdateTags(seriesWithUpdatedTags);
        }

        private void TryCleanSeriesLibrary()
        {
            if (_configService.ListSyncLevel == ListSyncLevelType.Disabled)
            {
                return;
            }

            if (AllListsSuccessfulWithAPendingClean())
            {
                CleanSeriesLibrary();
            }
        }

        private void CleanSeriesLibrary()
        {
            if (_configService.ListSyncLevel == ListSyncLevelType.Disabled)
            {
                return;
            }

            var seriesToUpdate = new List<Series>();
            var seriesInLibrary = _seriesService.GetAllSeries();
            var allListItems = _importListItemService.All();

            foreach (var series in seriesInLibrary)
            {
                var seriesExists = allListItems.Where(l =>
                    l.TvdbId == series.TvdbId ||
                    (l.ImdbId.IsNotNullOrWhiteSpace() && series.ImdbId.IsNotNullOrWhiteSpace() && l.ImdbId == series.ImdbId) ||
                    l.TmdbId == series.TmdbId ||
                    series.MalIds.Contains(l.MalId) ||
                    series.AniListIds.Contains(l.AniListId)).ToList();

                if (!seriesExists.Any())
                {
                    switch (_configService.ListSyncLevel)
                    {
                        case ListSyncLevelType.LogOnly:
                            _logger.Info("{0} was in your library, but not found in your lists --> You might want to unmonitor or remove it", series);
                            break;
                        case ListSyncLevelType.KeepAndUnmonitor when series.Monitored:
                            _logger.Info("{0} was in your library, but not found in your lists --> Keeping in library but unmonitoring it", series);
                            series.Monitored = false;
                            seriesToUpdate.Add(series);
                            break;
                        case ListSyncLevelType.KeepAndTag when !series.Tags.Contains(_configService.ListSyncTag):
                            _logger.Info("{0} was in your library, but not found in your lists --> Keeping in library but tagging it", series);
                            series.Tags.Add(_configService.ListSyncTag);
                            seriesToUpdate.Add(series);
                            break;
                        default:
                            break;
                    }
                }
            }

            _seriesService.UpdateSeries(seriesToUpdate, true);
            _importListStatusService.MarkListsAsCleaned();
        }

        // ---------------------------------------------------------------- MOVIES pipeline

        private void ProcessMovieReport(ImportListDefinition importList, ImportListMovie report, List<ImportListExclusion> listExclusions, List<int> dbMovies, List<Movie> moviesToAdd)
        {
            if (report.TmdbId == 0 || !importList.EnableAuto)
            {
                return;
            }

            // Check to see if movie in DB
            if (dbMovies.Contains(report.TmdbId))
            {
                _logger.Debug("{0} [{1}] Rejected, Movie Exists in DB", report.TmdbId, report.Title);
                return;
            }

            // Check to see if movie excluded
            var excludedMovie = listExclusions.SingleOrDefault(s => s.TmdbId == report.TmdbId);

            if (excludedMovie != null)
            {
                _logger.Debug("{0} [{1}] Rejected due to list exclusion", report.TmdbId, report.Title);
                return;
            }

            // Append Movie if not already in DB or already on add list
            if (moviesToAdd.All(s => s.TmdbId != report.TmdbId))
            {
                var monitorType = importList.Monitor;

                moviesToAdd.Add(new Movie
                {
                    Monitored = monitorType != MovieMonitorTypes.None,
                    RootFolderPath = importList.RootFolderPath,
                    QualityProfileId = importList.QualityProfileId,
                    MinimumAvailability = importList.MinimumAvailability,
                    Tags = importList.Tags,
                    TmdbId = report.TmdbId,
                    Title = report.Title,
                    Year = report.Year,
                    ImdbId = report.ImdbId,
                    AddOptions = new AddMovieOptions
                    {
                        SearchForMovie = monitorType != MovieMonitorTypes.None && importList.SearchOnAdd,
                        Monitor = monitorType,
                        AddMethod = AddMovieMethod.List
                    }
                });
            }
        }

        private void ProcessMovieListItems(ImportListFetchResult listFetchResult)
        {
            listFetchResult.Movies = listFetchResult.Movies.DistinctBy(x =>
            {
                if (x.TmdbId != 0)
                {
                    return x.TmdbId.ToString();
                }

                if (x.ImdbId.IsNotNullOrWhiteSpace())
                {
                    return x.ImdbId;
                }

                return x.Title;
            }).ToList();

            var listedMovies = listFetchResult.Movies.ToList();

            if (!listedMovies.Any())
            {
                return;
            }

            var importExclusions = _importListExclusionService.All();
            var dbMovies = _movieService.AllMovieTmdbIds();
            var moviesToAdd = new List<Movie>();

            var groupedMovies = listedMovies.GroupBy(x => x.ListId);

            foreach (var list in groupedMovies)
            {
                var importList = _importListFactory.Get(list.Key);

                foreach (var movie in list)
                {
                    if (movie.TmdbId != 0)
                    {
                        ProcessMovieReport(importList, movie, importExclusions, dbMovies, moviesToAdd);
                    }
                }
            }

            if (moviesToAdd.Any())
            {
                _logger.ProgressInfo("Adding {0} movies from your auto enabled lists to library", moviesToAdd.Count);
                _addMovieService.AddMovies(moviesToAdd, true);
            }
        }

        private void CleanMovieLibrary()
        {
            if (_configService.ListSyncLevel == ListSyncLevelType.Disabled)
            {
                return;
            }

            var listMovies = _listMovieService.GetAllListMovies();
            var moviesInLibrary = _movieService.GetAllMovies();

            var moviesToUpdate = new List<Movie>();

            foreach (var movie in moviesInLibrary)
            {
                var movieExists = listMovies.Any(c =>
                    c.TmdbId == movie.TmdbId ||
                    (c.ImdbId.IsNotNullOrWhiteSpace() && movie.ImdbId.IsNotNullOrWhiteSpace() && c.ImdbId == movie.ImdbId));

                if (!movieExists)
                {
                    switch (_configService.ListSyncLevel)
                    {
                        case ListSyncLevelType.LogOnly:
                            _logger.Info("{0} was in your library, but not found in your lists --> You might want to unmonitor or remove it", movie);
                            break;
                        case ListSyncLevelType.KeepAndUnmonitor:
                            _logger.Info("{0} was in your library, but not found in your lists --> Keeping in library but Unmonitoring it", movie);
                            movie.Monitored = false;
                            moviesToUpdate.Add(movie);
                            break;
                        case ListSyncLevelType.RemoveAndKeep:
                            _logger.Info("{0} was in your library, but not found in your lists --> Removing from library (keeping files)", movie);
                            _movieService.DeleteMovie(movie.Id, false);
                            break;
                        case ListSyncLevelType.RemoveAndDelete:
                            _logger.Info("{0} was in your library, but not found in your lists --> Removing from library and deleting files", movie);
                            _movieService.DeleteMovie(movie.Id, true);
                            break;
                    }
                }
            }

            _movieService.UpdateMovie(moviesToUpdate, true);
        }
    }
}
