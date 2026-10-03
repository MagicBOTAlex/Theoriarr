using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Blocklisting
{
    public interface IBlocklistRepository : IBasicRepository<Blocklist>
    {
        List<Blocklist> BlocklistedByTitle(int seriesId, string sourceTitle);
        List<Blocklist> BlocklistedByTorrentInfoHash(int seriesId, string torrentInfoHash);
        List<Blocklist> BlocklistedBySeries(int seriesId);
        void DeleteForSeriesIds(List<int> seriesIds);

        // D3: movie rows share the "Blocklist" table. Movie lookups key on MovieId.
        List<Blocklist> BlocklistedByTitleForMovie(int movieId, string sourceTitle);
        List<Blocklist> BlocklistedByTorrentInfoHashForMovie(int movieId, string torrentInfoHash);
        List<Blocklist> BlocklistedByMovie(int movieId);
        void DeleteForMovies(List<int> movieIds);
        PagingSpec<Blocklist> GetPaged(PagingSpec<Blocklist> pagingSpec, MediaType mediaType);
    }

    public class BlocklistRepository : BasicRepository<Blocklist>, IBlocklistRepository
    {
        public BlocklistRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Blocklist> BlocklistedByTitle(int seriesId, string sourceTitle)
        {
            return Query(e => e.SeriesId == seriesId && e.SourceTitle.Contains(sourceTitle));
        }

        public List<Blocklist> BlocklistedByTorrentInfoHash(int seriesId, string torrentInfoHash)
        {
            return Query(e => e.SeriesId == seriesId && e.TorrentInfoHash.Contains(torrentInfoHash));
        }

        public List<Blocklist> BlocklistedBySeries(int seriesId)
        {
            return Query(b => b.SeriesId == seriesId);
        }

        public void DeleteForSeriesIds(List<int> seriesIds)
        {
            Delete(x => seriesIds.Contains(x.SeriesId));
        }

        public List<Blocklist> BlocklistedByTitleForMovie(int movieId, string sourceTitle)
        {
            return Query(e => e.MovieId == movieId && e.SourceTitle.Contains(sourceTitle));
        }

        public List<Blocklist> BlocklistedByTorrentInfoHashForMovie(int movieId, string torrentInfoHash)
        {
            return Query(e => e.MovieId == movieId && e.TorrentInfoHash.Contains(torrentInfoHash));
        }

        public List<Blocklist> BlocklistedByMovie(int movieId)
        {
            var builder = Builder().Join<Blocklist, Movie>((h, a) => h.MovieId == a.Id)
                                   .Where<Blocklist>(h => h.MovieId == movieId);

            return _database.QueryJoined<Blocklist, Movie>(builder, (blocklist, movie) =>
            {
                blocklist.Movie = movie;
                return blocklist;
            }).OrderByDescending(h => h.Date).ToList();
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(x => movieIds.Contains(x.MovieId));
        }

        public override PagingSpec<Blocklist> GetPaged(PagingSpec<Blocklist> pagingSpec)
        {
            var sortingByQuality = string.Equals(pagingSpec.SortKey, "quality", StringComparison.OrdinalIgnoreCase);
            var customSortExpression = sortingByQuality ? "COALESCE(\"r\".\"Score\", -1)" : null;

            pagingSpec.Records = GetPagedRecords(PagedBuilder(sortingByQuality), pagingSpec, PagedQuery, customSortExpression);

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(Blocklist))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(PagedBuilder(sortingByQuality).Select(typeof(Blocklist)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        public PagingSpec<Blocklist> GetPaged(PagingSpec<Blocklist> pagingSpec, MediaType mediaType)
        {
            if (mediaType == MediaType.Series)
            {
                return GetPaged(pagingSpec);
            }

            var builder = Builder()
                .Join<Blocklist, Movie>((b, m) => b.MovieId == m.Id)
                .LeftJoin<Movie, MovieMetadata>((m, mm) => m.MovieMetadataId == mm.Id);

            pagingSpec.Records = GetPagedRecords(builder, pagingSpec, b => _database.QueryJoined<Blocklist, Movie>(b, (blocklist, movie) =>
            {
                blocklist.Movie = movie;
                return blocklist;
            }));

            var countTemplate = $"SELECT COUNT(*) FROM (SELECT /**select**/ FROM \"{TableMapping.Mapper.TableNameMapping(typeof(Blocklist))}\" /**join**/ /**innerjoin**/ /**leftjoin**/ /**where**/ /**groupby**/ /**having**/) AS \"Inner\"";
            pagingSpec.TotalRecords = GetPagedRecordCount(builder.Select(typeof(Blocklist)), pagingSpec, countTemplate);

            return pagingSpec;
        }

        protected override SqlBuilder PagedBuilder() => PagedBuilder(false);

        private SqlBuilder PagedBuilder(bool joinQualityRanks)
        {
            var builder = Builder()
                .Join<Blocklist, Series>((b, m) => b.SeriesId == m.Id);

            if (joinQualityRanks)
            {
                var qualityIdExpr = _database.DatabaseType == DatabaseType.PostgreSQL
                    ? "(\"Blocklist\".\"Quality\"::jsonb ->> 'quality')::int"
                    : "json_extract(\"Blocklist\".\"Quality\", '$.quality')";

                builder.LeftJoin(
                    $"\"QualityProfileQualityRanks\" AS \"r\" " +
                    $"ON \"r\".\"ProfileId\" = \"Series\".\"QualityProfileId\" " +
                    $"AND \"r\".\"QualityId\" = {qualityIdExpr}");
            }

            return builder;
        }

        protected override IEnumerable<Blocklist> PagedQuery(SqlBuilder builder) =>
            _database.QueryJoined<Blocklist, Series>(builder, (blocklist, series) =>
            {
                blocklist.Series = series;
                return blocklist;
            });
    }
}
