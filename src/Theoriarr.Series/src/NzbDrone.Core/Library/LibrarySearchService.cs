using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Library
{
    public interface ILibrarySearchService
    {
        List<LibrarySearchResult> Search(string term);
        void Invalidate();
    }

    /// <summary>
    /// Server-side substring search over the unified library, backed by an embedded
    /// Lucene.NET index (RAM directory) built from series + movies.
    /// </summary>
    public class LibrarySearchService : ILibrarySearchService
    {
        private const int MaxResults = 5000;

        private static readonly TimeSpan IndexLifetime = TimeSpan.FromSeconds(30);

        // field name -> relevance boost (title first, overview last)
        private static readonly (string Field, float Boost)[] Fields =
        {
            ("title", 8f),
            ("sortTitle", 7f),
            ("originalTitle", 5f),
            ("alternateTitles", 4f),
            ("year", 2f),
            ("overview", 1f)
        };

        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly object _lock = new object();

        private RAMDirectory _directory;
        private DirectoryReader _reader;
        private IndexSearcher _searcher;
        private DateTime _builtAt = DateTime.MinValue;

        public LibrarySearchService(ISeriesService seriesService,
                                    IMovieService movieService,
                                    ISceneMappingService sceneMappingService)
        {
            _seriesService = seriesService;
            _movieService = movieService;
            _sceneMappingService = sceneMappingService;
        }

        public List<LibrarySearchResult> Search(string term)
        {
            if (term.IsNullOrWhiteSpace())
            {
                return new List<LibrarySearchResult>();
            }

            var needle = Normalize(term);

            if (needle.Length == 0)
            {
                return new List<LibrarySearchResult>();
            }

            var searcher = GetSearcher();
            var query = BuildQuery(needle);
            var topDocs = searcher.Search(query, MaxResults);

            var results = new List<LibrarySearchResult>(topDocs.ScoreDocs.Length);

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var document = searcher.Doc(scoreDoc.Doc);

                results.Add(new LibrarySearchResult
                {
                    MediaType = document.Get("mediaType") == "movie" ? MediaType.Movie : MediaType.Series,
                    Id = int.Parse(document.Get("id"))
                });
            }

            return results;
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                DisposeIndex();
                _builtAt = DateTime.MinValue;
            }
        }

        private IndexSearcher GetSearcher()
        {
            lock (_lock)
            {
                if (_searcher == null || DateTime.UtcNow - _builtAt > IndexLifetime)
                {
                    BuildIndex();
                }

                return _searcher;
            }
        }

        private void BuildIndex()
        {
            DisposeIndex();

            var directory = new RAMDirectory();
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, new KeywordAnalyzer())
            {
                OpenMode = OpenMode.CREATE
            };

            using (var writer = new IndexWriter(directory, config))
            {
                foreach (var series in _seriesService.GetAllSeries())
                {
                    var alternateTitles = string.Join(' ',
                        _sceneMappingService.FindByTvdbId(series.TvdbId).Select(m => m.Title));

                    var document = CreateDocument("series", series.Id);
                    AddField(document, "title", series.Title);
                    AddField(document, "sortTitle", series.SortTitle);
                    AddField(document, "originalTitle", null);
                    AddField(document, "alternateTitles", alternateTitles);
                    AddField(document, "year", series.Year.ToString());
                    AddField(document, "overview", series.Overview);
                    writer.AddDocument(document);
                }

                foreach (var movie in _movieService.GetAllMovies())
                {
                    var metadata = movie.MovieMetadata?.Value;
                    var alternateTitles = string.Join(' ',
                        metadata?.AlternativeTitles?.Select(t => t.Title) ?? Enumerable.Empty<string>());

                    var document = CreateDocument("movie", movie.Id);
                    AddField(document, "title", movie.Title);
                    AddField(document, "sortTitle", metadata?.SortTitle);
                    AddField(document, "originalTitle", metadata?.OriginalTitle);
                    AddField(document, "alternateTitles", alternateTitles);
                    AddField(document, "year", movie.Year.ToString());
                    AddField(document, "overview", metadata?.Overview);
                    writer.AddDocument(document);
                }
            }

            _directory = directory;
            _reader = DirectoryReader.Open(directory);
            _searcher = new IndexSearcher(_reader);
            _builtAt = DateTime.UtcNow;
        }

        private static Document CreateDocument(string mediaType, int id)
        {
            return new Document
            {
                new StringField("mediaType", mediaType, Field.Store.YES),
                new StringField("id", id.ToString(), Field.Store.YES)
            };
        }

        private static void AddField(Document document, string field, string value)
        {
            var normalized = Normalize(value);

            if (normalized.Length > 0)
            {
                document.Add(new StringField(field, normalized, Field.Store.NO));
            }
        }

        private static Query BuildQuery(string needle)
        {
            var escaped = QueryParserBase.Escape(needle);
            var query = new BooleanQuery();

            foreach (var (field, boost) in Fields)
            {
                // "contains" match (infix) plus a higher-boosted "starts with" match.
                query.Add(new WildcardQuery(new Term(field, "*" + escaped + "*")) { Boost = boost }, Occur.SHOULD);
                query.Add(new PrefixQuery(new Term(field, escaped)) { Boost = boost * 3 }, Occur.SHOULD);
            }

            return query;
        }

        private static string Normalize(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            return value.RemoveDiacritics().ToLowerInvariant().Trim();
        }

        private void DisposeIndex()
        {
            _reader?.Dispose();
            _reader = null;
            _searcher = null;
            _directory?.Dispose();
            _directory = null;
        }
    }
}
