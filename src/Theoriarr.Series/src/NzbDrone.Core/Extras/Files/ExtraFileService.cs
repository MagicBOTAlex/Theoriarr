using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Extras.Files
{
    public interface IExtraFileService<TExtraFile>
        where TExtraFile : ExtraFile, new()
    {
        List<TExtraFile> GetFilesBySeries(int seriesId);
        List<TExtraFile> GetFilesByEpisodeFile(int episodeFileId);
        List<TExtraFile> GetFilesByMovie(int movieId);
        List<TExtraFile> GetFilesByMovieFile(int movieFileId);
        TExtraFile FindBySeriesPath(int seriesId, string path);
        TExtraFile FindByMoviePath(int movieId, string path);
        void Upsert(TExtraFile extraFile);
        void Upsert(List<TExtraFile> extraFiles);
        void Delete(int id);
        void DeleteMany(IEnumerable<int> ids);
    }

    public abstract class ExtraFileService<TExtraFile> : IExtraFileService<TExtraFile>,
                                                         IHandleAsync<SeriesDeletedEvent>,
                                                         IHandle<EpisodeFileDeletedEvent>,
                                                         IHandleAsync<MoviesDeletedEvent>,
                                                         IHandleAsync<MovieFileDeletedEvent>
        where TExtraFile : ExtraFile, new()
    {
        private readonly IExtraFileRepository<TExtraFile> _repository;
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly IDiskProvider _diskProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly Logger _logger;

        public ExtraFileService(IExtraFileRepository<TExtraFile> repository,
                                ISeriesService seriesService,
                                IMovieService movieService,
                                IDiskProvider diskProvider,
                                IRecycleBinProvider recycleBinProvider,
                                Logger logger)
        {
            _repository = repository;
            _seriesService = seriesService;
            _movieService = movieService;
            _diskProvider = diskProvider;
            _recycleBinProvider = recycleBinProvider;
            _logger = logger;
        }

        public List<TExtraFile> GetFilesBySeries(int seriesId)
        {
            return _repository.GetFilesBySeries(seriesId);
        }

        public List<TExtraFile> GetFilesByEpisodeFile(int episodeFileId)
        {
            return _repository.GetFilesByEpisodeFile(episodeFileId);
        }

        public List<TExtraFile> GetFilesByMovie(int movieId)
        {
            return _repository.GetFilesByMovie(movieId);
        }

        public List<TExtraFile> GetFilesByMovieFile(int movieFileId)
        {
            return _repository.GetFilesByMovieFile(movieFileId);
        }

        public TExtraFile FindBySeriesPath(int seriesId, string path)
        {
            return _repository.FindBySeriesPath(seriesId, path);
        }

        public TExtraFile FindByMoviePath(int movieId, string path)
        {
            return _repository.FindByMoviePath(movieId, path);
        }

        public void Upsert(TExtraFile extraFile)
        {
            Upsert(new List<TExtraFile> { extraFile });
        }

        public void Upsert(List<TExtraFile> extraFiles)
        {
            extraFiles.ForEach(m =>
            {
                m.LastUpdated = DateTime.UtcNow;

                if (m.Id == 0)
                {
                    m.Added = m.LastUpdated;
                }
            });

            _repository.InsertMany(extraFiles.Where(m => m.Id == 0).ToList());
            _repository.UpdateMany(extraFiles.Where(m => m.Id > 0).ToList());
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
        }

        public void DeleteMany(IEnumerable<int> ids)
        {
            _repository.DeleteMany(ids);
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            _logger.Debug("Deleting Extra from database for series: {0}", string.Join(',', message.Series));
            _repository.DeleteForSeriesIds(message.Series.Select(m => m.Id).ToList());
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            var episodeFile = message.EpisodeFile;

            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing episode file from DB as part of cleanup routine, not deleting extra files from disk.");
            }
            else
            {
                var series = _seriesService.GetSeries(message.EpisodeFile.SeriesId);

                foreach (var extra in _repository.GetFilesByEpisodeFile(episodeFile.Id))
                {
                    var path = Path.Combine(series.Path, extra.RelativePath);

                    if (_diskProvider.FileExists(path))
                    {
                        // Send to the recycling bin so they can be recovered if necessary
                        var subfolder = _diskProvider.GetParentFolder(series.Path).GetRelativePath(_diskProvider.GetParentFolder(path));
                        _recycleBinProvider.DeleteFile(path, subfolder);
                    }
                }
            }

            _logger.Debug("Deleting Extra from database for episode file: {0}", episodeFile);
            _repository.DeleteForEpisodeFile(episodeFile.Id);
        }

        public void HandleAsync(MoviesDeletedEvent message)
        {
            _logger.Debug("Deleting Extra from database for movies: {0}", string.Join(',', message.Movies));
            _repository.DeleteForMovies(message.Movies.Select(m => m.Id).ToList());
        }

        public void HandleAsync(MovieFileDeletedEvent message)
        {
            var movieFile = message.MovieFile;

            if (message.Reason == DeleteMediaFileReason.NoLinkedEpisodes)
            {
                _logger.Debug("Removing movie file from DB as part of cleanup routine, not deleting extra files from disk.");
            }
            else
            {
                var movie = _movieService.GetMovie(message.MovieFile.MovieId);

                foreach (var extra in _repository.GetFilesByMovieFile(movieFile.Id))
                {
                    var path = Path.Combine(movie.Path, extra.RelativePath);

                    if (_diskProvider.FileExists(path))
                    {
                        // Send to the recycling bin so they can be recovered if necessary
                        var subfolder = _diskProvider.GetParentFolder(movie.Path).GetRelativePath(_diskProvider.GetParentFolder(path));
                        _recycleBinProvider.DeleteFile(path, subfolder);
                    }
                }
            }

            _logger.Debug("Deleting Extra from database for movie file: {0}", movieFile);
            _repository.DeleteForMovieFile(movieFile.Id);
        }
    }
}
