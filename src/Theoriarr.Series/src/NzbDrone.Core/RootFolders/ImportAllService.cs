using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.RootFolders.Commands;
using NzbDrone.Core.Tv;
using TvMonitorTypes = NzbDrone.Core.Tv.MonitorTypes;

namespace NzbDrone.Core.RootFolders
{
    // Walks every root folder, matches each unmapped subfolder against the metadata
    // providers and adds the best match to the library. Adding a series/movie publishes
    // an added event which queues a Refresh*Command, so the files themselves are then
    // scanned and imported without a second manual step.
    public class ImportAllService : IExecute<ImportAllCommand>
    {
        private readonly IRootFolderService _rootFolderService;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly ISearchForNewSeries _searchForNewSeries;
        private readonly ISearchForNewMovie _searchForNewMovie;
        private readonly IAddSeriesService _addSeriesService;
        private readonly IAddMovieService _addMovieService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly Logger _logger;

        public ImportAllService(IRootFolderService rootFolderService,
                                ISeriesService seriesService,
                                IMovieService movieService,
                                ISearchForNewSeries searchForNewSeries,
                                ISearchForNewMovie searchForNewMovie,
                                IAddSeriesService addSeriesService,
                                IAddMovieService addMovieService,
                                IQualityProfileService qualityProfileService,
                                Logger logger)
        {
            _rootFolderService = rootFolderService;
            _seriesService = seriesService;
            _movieService = movieService;
            _searchForNewSeries = searchForNewSeries;
            _searchForNewMovie = searchForNewMovie;
            _addSeriesService = addSeriesService;
            _addMovieService = addMovieService;
            _qualityProfileService = qualityProfileService;
            _logger = logger;
        }

        public void Execute(ImportAllCommand message)
        {
            var qualityProfileId = _qualityProfileService.All().FirstOrDefault()?.Id ?? 0;

            var rootFolders = _rootFolderService.AllWithUnmappedFolders();

            var seriesPaths = new HashSet<string>(_seriesService.GetAllSeriesPaths().Values, PathEqualityComparer.Instance);
            var moviePaths = new HashSet<string>(_movieService.AllMoviePaths().Values, PathEqualityComparer.Instance);
            var existingTvdbIds = new HashSet<int>(_seriesService.AllSeriesTvdbIds().Values);
            var existingTmdbIds = new HashSet<int>(_movieService.AllMovieTmdbIds());

            var seriesToAdd = new List<Series>();
            var moviesToAdd = new List<Movie>();

            foreach (var rootFolder in rootFolders)
            {
                if (!rootFolder.Accessible || rootFolder.UnmappedFolders == null)
                {
                    continue;
                }

                var isMovieFolder = rootFolder.MediaType == MediaType.Movie;

                foreach (var unmappedFolder in rootFolder.UnmappedFolders)
                {
                    try
                    {
                        if (isMovieFolder)
                        {
                            AddMovie(rootFolder, unmappedFolder, qualityProfileId, moviePaths, existingTmdbIds, moviesToAdd);
                        }
                        else
                        {
                            AddSeries(rootFolder, unmappedFolder, qualityProfileId, seriesPaths, existingTvdbIds, seriesToAdd);
                        }
                    }

                    // One bad folder (lookup failure, unreadable path, etc.) must not abort
                    // the whole import.
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Unable to import unmapped folder {0}", unmappedFolder.Path);
                    }
                }
            }

            if (seriesToAdd.Any())
            {
                _logger.Info("Import All: adding {0} series", seriesToAdd.Count);
                _addSeriesService.AddSeries(seriesToAdd, true);
            }

            if (moviesToAdd.Any())
            {
                _logger.Info("Import All: adding {0} movies", moviesToAdd.Count);
                _addMovieService.AddMovies(moviesToAdd, true);
            }

            if (!seriesToAdd.Any() && !moviesToAdd.Any())
            {
                _logger.Info("Import All: no new series or movies found");
            }
        }

        private void AddSeries(RootFolder rootFolder,
                               UnmappedFolder unmappedFolder,
                               int qualityProfileId,
                               HashSet<string> seriesPaths,
                               HashSet<int> existingTvdbIds,
                               List<Series> seriesToAdd)
        {
            if (seriesPaths.Contains(unmappedFolder.Path))
            {
                return;
            }

            var match = _searchForNewSeries.SearchForNewSeries(unmappedFolder.Name).FirstOrDefault();

            if (match == null || match.TvdbId <= 0)
            {
                _logger.Debug("Import All: no series match for {0}", unmappedFolder.Name);
                return;
            }

            if (existingTvdbIds.Contains(match.TvdbId) || seriesToAdd.Any(s => s.TvdbId == match.TvdbId))
            {
                return;
            }

            var seriesType = rootFolder.MediaType == MediaType.Anime || match.SeriesType == SeriesTypes.Anime
                ? SeriesTypes.Anime
                : SeriesTypes.Standard;

            seriesToAdd.Add(new Series
            {
                TvdbId = match.TvdbId,
                Title = match.Title,
                Year = match.Year,
                Path = unmappedFolder.Path,
                RootFolderPath = rootFolder.Path,
                QualityProfileId = qualityProfileId,
                Monitored = true,
                MonitorNewItems = NewItemMonitorTypes.All,
                SeasonFolder = true,
                SeriesType = seriesType,
                Tags = new HashSet<int>(),
                AddOptions = new AddSeriesOptions
                {
                    Monitor = TvMonitorTypes.All,
                    SearchForMissingEpisodes = false,
                    SearchForCutoffUnmetEpisodes = false
                }
            });
        }

        private void AddMovie(RootFolder rootFolder,
                              UnmappedFolder unmappedFolder,
                              int qualityProfileId,
                              HashSet<string> moviePaths,
                              HashSet<int> existingTmdbIds,
                              List<Movie> moviesToAdd)
        {
            if (moviePaths.Contains(unmappedFolder.Path))
            {
                return;
            }

            var match = _searchForNewMovie.SearchForNewMovie(unmappedFolder.Name).FirstOrDefault();

            if (match == null || match.TmdbId <= 0)
            {
                _logger.Debug("Import All: no movie match for {0}", unmappedFolder.Name);
                return;
            }

            if (existingTmdbIds.Contains(match.TmdbId) || moviesToAdd.Any(m => m.TmdbId == match.TmdbId))
            {
                return;
            }

            moviesToAdd.Add(new Movie
            {
                TmdbId = match.TmdbId,
                Path = unmappedFolder.Path,
                RootFolderPath = rootFolder.Path,
                QualityProfileId = qualityProfileId,
                Monitored = true,
                MinimumAvailability = MovieStatusType.Released,
                Tags = new HashSet<int>(),
                AddOptions = new AddMovieOptions
                {
                    SearchForMovie = false,
                    AddMethod = AddMovieMethod.Manual
                }
            });
        }
    }
}
