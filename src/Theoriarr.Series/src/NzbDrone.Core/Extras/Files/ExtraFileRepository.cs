using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Extras.Files
{
    public interface IExtraFileRepository<TExtraFile> : IBasicRepository<TExtraFile>
        where TExtraFile : ExtraFile, new()
    {
        void DeleteForSeriesIds(List<int> seriesIds);
        void DeleteForSeason(int seriesId, int seasonNumber);
        void DeleteForEpisodeFile(int episodeFileId);
        List<TExtraFile> GetFilesBySeries(int seriesId);
        List<TExtraFile> GetFilesBySeason(int seriesId, int seasonNumber);
        List<TExtraFile> GetFilesByEpisodeFile(int episodeFileId);
        TExtraFile FindBySeriesPath(int seriesId, string path);

        void DeleteForMovies(List<int> movieIds);
        void DeleteForMovieFile(int movieFileId);
        List<TExtraFile> GetFilesByMovie(int movieId);
        List<TExtraFile> GetFilesByMovieFile(int movieFileId);
        TExtraFile FindByMoviePath(int movieId, string path);
    }

    public class ExtraFileRepository<TExtraFile> : BasicRepository<TExtraFile>, IExtraFileRepository<TExtraFile>
        where TExtraFile : ExtraFile, new()
    {
        public ExtraFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void DeleteForSeriesIds(List<int> seriesIds)
        {
            Delete(c => c.SeriesId.HasValue && seriesIds.Contains(c.SeriesId.Value));
        }

        public void DeleteForSeason(int seriesId, int seasonNumber)
        {
            Delete(c => c.SeriesId == seriesId && c.SeasonNumber == seasonNumber);
        }

        public void DeleteForEpisodeFile(int episodeFileId)
        {
            Delete(c => c.EpisodeFileId == episodeFileId);
        }

        public List<TExtraFile> GetFilesBySeries(int seriesId)
        {
            return Query(c => c.SeriesId == seriesId);
        }

        public List<TExtraFile> GetFilesBySeason(int seriesId, int seasonNumber)
        {
            return Query(c => c.SeriesId == seriesId && c.SeasonNumber == seasonNumber);
        }

        public List<TExtraFile> GetFilesByEpisodeFile(int episodeFileId)
        {
            return Query(c => c.EpisodeFileId == episodeFileId);
        }

        public TExtraFile FindBySeriesPath(int seriesId, string path)
        {
            return Query(c => c.SeriesId == seriesId && c.RelativePath == path).SingleOrDefault();
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(c => c.MovieId.HasValue && movieIds.Contains(c.MovieId.Value));
        }

        public void DeleteForMovieFile(int movieFileId)
        {
            Delete(c => c.MovieFileId == movieFileId);
        }

        public List<TExtraFile> GetFilesByMovie(int movieId)
        {
            return Query(c => c.MovieId == movieId);
        }

        public List<TExtraFile> GetFilesByMovieFile(int movieFileId)
        {
            return Query(c => c.MovieFileId == movieFileId);
        }

        public TExtraFile FindByMoviePath(int movieId, string path)
        {
            return Query(c => c.MovieId == movieId && c.RelativePath == path).SingleOrDefault();
        }
    }
}
