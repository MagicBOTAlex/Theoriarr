using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.History
{
    public interface IHistoryRepository : IBasicRepository<EpisodeHistory>
    {
        EpisodeHistory MostRecentForEpisode(int episodeId);
        List<EpisodeHistory> FindByEpisodeId(int episodeId);
        EpisodeHistory MostRecentForDownloadId(string downloadId);
        List<EpisodeHistory> FindByDownloadId(string downloadId);
        List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType);
        List<EpisodeHistory> FindDownloadHistory(int idSeriesId, QualityModel quality);
        void DeleteForSeries(List<int> seriesIds);
        List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType);
        PagingSpec<EpisodeHistory> GetPaged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities);

        // D3: movie rows share the "History" table with episode rows. Movie methods are
        // kept distinct from the episode methods to avoid return-type-only overloads.
        List<QualityModel> GetBestQualityInHistory(int movieId);
        MovieHistory MostRecentForMovie(int movieId);
        MovieHistory GetMovie(int historyId);
        List<MovieHistory> FindByMovieId(int movieId);
        MovieHistory MostRecentMovieForDownloadId(string downloadId);
        List<MovieHistory> FindMovieByDownloadId(string downloadId);
        List<MovieHistory> FindMovieDownloadHistory(int movieId, QualityModel quality);
        List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType);
        void DeleteForMovies(List<int> movieIds);
        List<MovieHistory> SinceMovie(DateTime date, MovieHistoryEventType? eventType);
        PagingSpec<MovieHistory> GetPaged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities);
        MovieHistory Insert(MovieHistory model);
        void UpdateMany(List<MovieHistory> models);
    }

    public class HistoryRepository : BasicRepository<EpisodeHistory>, IHistoryRepository
    {
        private readonly MovieHistoryPagingRepository _movieRepository;
        private readonly IMainDatabase _mainDatabase;
        private readonly IEventAggregator _events;

        public HistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : this(database, eventAggregator, new MovieHistoryPagingRepository(database, eventAggregator))
        {
        }

        private HistoryRepository(IMainDatabase database, IEventAggregator eventAggregator, MovieHistoryPagingRepository movieRepository)
            : base(database, eventAggregator)
        {
            _mainDatabase = database;
            _events = eventAggregator;
            _movieRepository = movieRepository;
        }

        public EpisodeHistory MostRecentForEpisode(int episodeId)
        {
            return Query(h => h.EpisodeId == episodeId).MaxBy(h => h.Date);
        }

        public List<EpisodeHistory> FindByEpisodeId(int episodeId)
        {
            return Query(h => h.EpisodeId == episodeId)
                        .OrderByDescending(h => h.Date)
                        .ToList();
        }

        public EpisodeHistory MostRecentForDownloadId(string downloadId)
        {
            return Query(h => h.DownloadId == downloadId && h.MediaType != MediaType.Movie).MaxBy(h => h.Date);
        }

        public List<EpisodeHistory> FindByDownloadId(string downloadId)
        {
            // D3: History is a shared superset table; without the discriminator an episode
            // lookup also returns movie rows (and vice versa). Only return episode rows.
            return Query(h => h.DownloadId == downloadId && h.MediaType != MediaType.Movie);
        }

        public List<EpisodeHistory> GetBySeries(int seriesId, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder().Join<EpisodeHistory, Series>((h, a) => h.SeriesId == a.Id)
                                   .Join<EpisodeHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                                   .Where<EpisodeHistory>(h => h.SeriesId == seriesId);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return Query(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> GetBySeason(int seriesId, int seasonNumber, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Join<EpisodeHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Where<EpisodeHistory>(h => h.SeriesId == seriesId && h.Episode.SeasonNumber == seasonNumber);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<EpisodeHistory, Episode>(
                builder,
                (history, episode) =>
                {
                    history.Episode = episode;
                    return history;
                }).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> GetByEpisode(int episodeId, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<EpisodeHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Where<EpisodeHistory>(h => h.EpisodeId == episodeId);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return Query(builder).OrderByDescending(h => h.Date).ToList();
        }

        public List<EpisodeHistory> FindDownloadHistory(int idSeriesId, QualityModel quality)
        {
            return Query(h =>
                 h.SeriesId == idSeriesId &&
                 h.Quality == quality &&
                 (h.EventType == EpisodeHistoryEventType.Grabbed ||
                 h.EventType == EpisodeHistoryEventType.DownloadFailed ||
                 h.EventType == EpisodeHistoryEventType.DownloadFolderImported))
                 .ToList();
        }

        public void DeleteForSeries(List<int> seriesIds)
        {
            Delete(c => seriesIds.Contains(c.SeriesId));
        }

        public List<EpisodeHistory> Since(DateTime date, EpisodeHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<EpisodeHistory, Episode>((h, a) => h.EpisodeId == a.Id)
                .Where<EpisodeHistory>(x => x.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<EpisodeHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<EpisodeHistory, Series, Episode>(builder, (history, series, episode) =>
            {
                history.Series = series;
                history.Episode = episode;
                return history;
            }).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<EpisodeHistory> GetPaged(PagingSpec<EpisodeHistory> pagingSpec, int[] languages, int[] qualities)
        {
            var sortingByQuality = string.Equals(pagingSpec.SortKey, "quality", StringComparison.OrdinalIgnoreCase);
            var customSortExpression = sortingByQuality ? "COALESCE(\"r\".\"Score\", -1)" : null;

            pagingSpec.Records = GetPagedRecords(PagedBuilder(languages, qualities, sortingByQuality), pagingSpec, PagedQuery, customSortExpression);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(languages, qualities, sortingByQuality).Select(typeof(EpisodeHistory)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        private SqlBuilder PagedBuilder(int[] languages, int[] qualities, bool joinQualityRanks)
        {
            var builder = Builder()
                .Join<EpisodeHistory, Series>((h, a) => h.SeriesId == a.Id)
                .Join<EpisodeHistory, Episode>((h, a) => h.EpisodeId == a.Id);

            if (joinQualityRanks)
            {
                var qualityIdExpr = _database.DatabaseType == DatabaseType.PostgreSQL
                    ? "(\"History\".\"Quality\"::jsonb ->> 'quality')::int"
                    : "json_extract(\"History\".\"Quality\", '$.quality')";

                builder.LeftJoin(
                    $"\"QualityProfileQualityRanks\" AS \"r\" " +
                    $"ON \"r\".\"ProfileId\" = \"Series\".\"QualityProfileId\" " +
                    $"AND \"r\".\"QualityId\" = {qualityIdExpr}");
            }

            if (languages is { Length: > 0 })
            {
                builder.Where($"({BuildLanguageWhereClause(languages)})");
            }

            if (qualities is { Length: > 0 })
            {
                builder.Where($"({BuildQualityWhereClause(qualities)})");
            }

            return builder;
        }

        protected override IEnumerable<EpisodeHistory> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<EpisodeHistory, Series, Episode>(builder, (history, series, episode) =>
            {
                history.Series = series;
                history.Episode = episode;
                return history;
            });

        private string BuildLanguageWhereClause(int[] languages)
        {
            var clauses = new List<string>();

            foreach (var language in languages)
            {
                // There are 4 different types of values we should see:
                // - Not the last value in the array
                // - When it's the last value in the array and on different OSes
                // - When it was converted from a single language

                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\".\"Languages\" LIKE '[% {language},%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(13) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(10) || '%]'");
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\".\"Languages\" LIKE '[{language}]'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }

        private string BuildQualityWhereClause(int[] qualities)
        {
            var clauses = new List<string>();

            foreach (var quality in qualities)
            {
                clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(EpisodeHistory))}\".\"Quality\" LIKE '%_quality_: {quality},%'");
            }

            return $"({string.Join(" OR ", clauses)})";
        }

        public List<QualityModel> GetBestQualityInHistory(int movieId)
        {
            return _database.Query<MovieHistory>(Builder().Where<MovieHistory>(x => x.MovieId == movieId))
                            .Select(h => h.Quality)
                            .ToList();
        }

        public MovieHistory MostRecentForMovie(int movieId)
        {
            return _database.Query<MovieHistory>(Builder().Where<MovieHistory>(h => h.MovieId == movieId))
                            .MaxBy(h => h.Date);
        }

        public MovieHistory GetMovie(int historyId)
        {
            return _movieRepository.Get(historyId);
        }

        public List<MovieHistory> FindByMovieId(int movieId)
        {
            return _database.Query<MovieHistory>(Builder().Where<MovieHistory>(h => h.MovieId == movieId))
                            .OrderByDescending(h => h.Date)
                            .ToList();
        }

        public MovieHistory MostRecentMovieForDownloadId(string downloadId)
        {
            return FindMovieByDownloadId(downloadId).MaxBy(h => h.Date);
        }

        public List<MovieHistory> FindMovieByDownloadId(string downloadId)
        {
            // D3: only return movie rows; the downloadId may also match episode rows in the
            // shared "History" table.
            return _database.Query<MovieHistory>(Builder().Where<MovieHistory>(x => x.DownloadId == downloadId && x.MediaType == MediaType.Movie)).ToList();
        }

        public List<MovieHistory> FindMovieDownloadHistory(int movieId, QualityModel quality)
        {
            var allowed = new[]
            {
                (int)MovieHistoryEventType.Grabbed,
                (int)MovieHistoryEventType.DownloadFailed,
                (int)MovieHistoryEventType.DownloadFolderImported
            };

            return _database.Query<MovieHistory>(Builder().Where<MovieHistory>(h =>
                h.MovieId == movieId &&
                h.Quality == quality &&
                allowed.Contains((int)h.EventType))).ToList();
        }

        public List<MovieHistory> GetByMovieId(int movieId, MovieHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<MovieHistory, Movie>((h, m) => h.MovieId == m.Id)
                .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                .Where<MovieHistory>(h => h.MovieId == movieId);

            if (eventType.HasValue)
            {
                builder.Where<MovieHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<MovieHistory, Movie, QualityProfile>(
                builder,
                (history, movie, profile) =>
                {
                    history.Movie = movie;
                    history.Movie.QualityProfile = profile;
                    return history;
                }).OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(Builder().Where<MovieHistory>(c => movieIds.Contains(c.MovieId)));
        }

        public List<MovieHistory> SinceMovie(DateTime date, MovieHistoryEventType? eventType)
        {
            var builder = Builder()
                .Join<MovieHistory, Movie>((h, m) => h.MovieId == m.Id)
                .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                .Where<MovieHistory>(x => x.Date >= date);

            if (eventType.HasValue)
            {
                builder.Where<MovieHistory>(h => h.EventType == eventType);
            }

            return _database.QueryJoined<MovieHistory, Movie, QualityProfile>(
                builder,
                (history, movie, profile) =>
                {
                    history.Movie = movie;
                    history.Movie.QualityProfile = profile;
                    return history;
                }).OrderBy(h => h.Date).ToList();
        }

        public PagingSpec<MovieHistory> GetPaged(PagingSpec<MovieHistory> pagingSpec, int[] languages, int[] qualities)
        {
            // The history repository is a singleton, so per-request filters must not be
            // stored on a shared paging repository. Build a short-lived one instead.
            var pagingRepository = new MovieHistoryPagingRepository(_mainDatabase, _events, languages, qualities);

            return pagingRepository.GetPaged(pagingSpec);
        }

        public MovieHistory Insert(MovieHistory model)
        {
            return _movieRepository.Insert(model);
        }

        public void UpdateMany(List<MovieHistory> models)
        {
            _movieRepository.UpdateMany(models);
        }

        private class MovieHistoryPagingRepository : BasicRepository<MovieHistory>
        {
            private readonly int[] _languages;
            private readonly int[] _qualities;

            public MovieHistoryPagingRepository(IMainDatabase database, IEventAggregator eventAggregator)
                : this(database, eventAggregator, null, null)
            {
            }

            public MovieHistoryPagingRepository(IMainDatabase database, IEventAggregator eventAggregator, int[] languages, int[] qualities)
                : base(database, eventAggregator)
            {
                _languages = languages;
                _qualities = qualities;
            }

            protected override SqlBuilder PagedBuilder()
            {
                var builder = Builder()
                    .Join<MovieHistory, Movie>((h, m) => h.MovieId == m.Id)
                    .Join<Movie, QualityProfile>((m, p) => m.QualityProfileId == p.Id)
                    .Join<Movie, MovieMetadata>((m, md) => m.MovieMetadataId == md.Id);

                if (_languages is { Length: > 0 })
                {
                    builder.Where($"({BuildLanguageWhereClause(_languages)})");
                }

                if (_qualities is { Length: > 0 })
                {
                    builder.Where($"({BuildQualityWhereClause(_qualities)})");
                }

                return builder;
            }

            protected override IEnumerable<MovieHistory> PagedQuery(SqlBuilder builder) =>
                _database.QueryJoined<MovieHistory, Movie, QualityProfile>(builder, (history, movie, profile) =>
                {
                    history.Movie = movie;
                    history.Movie.QualityProfile = profile;
                    return history;
                });

            private string BuildLanguageWhereClause(int[] languages)
            {
                var clauses = new List<string>();

                foreach (var language in languages)
                {
                    clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language},%]'");
                    clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(13) || '%]'");
                    clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[% {language}' || CHAR(10) || '%]'");
                    clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Languages\" LIKE '[{language}]'");
                }

                return $"({string.Join(" OR ", clauses)})";
            }

            private string BuildQualityWhereClause(int[] qualities)
            {
                var clauses = new List<string>();

                foreach (var quality in qualities)
                {
                    clauses.Add($"\"{TableMapping.Mapper.TableNameMapping(typeof(MovieHistory))}\".\"Quality\" LIKE '%_quality_: {quality},%'");
                }

                return $"({string.Join(" OR ", clauses)})";
            }
        }
    }
}
