using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public interface ISeriesTitleMatcher
    {
        Series Match(string title);
        Series Match(string title, Series restrictTo);
        void Invalidate();
    }

    /// <summary>
    /// Fuzzy, full-text matching of a release's parsed title against every known XEM scene name and
    /// series title, backed by an embedded Lucene.NET index (RAM directory). This is the primary way
    /// alternative names are resolved: exact lookups and scene mappings still run first, but a
    /// release whose title uses an alias or formatting variant the exact paths cannot see is
    /// resolved here. Scene aliases match as substrings of the parsed title; series titles only match
    /// the whole parsed title (ignoring a trailing year) so a spin-off/sequel is never attached to
    /// the parent show whose name is merely a prefix of it.
    /// </summary>
    public class SeriesTitleMatcher : ISeriesTitleMatcher
    {
        private const string NameField = "name";
        private const string TitleField = "title";
        private const int MaxResults = 50;

        // Candidate terms are built from concatenations of adjacent words, so a multi-word alias
        // written without its space still matches its compact form. Bounded so we do not flood the
        // query with junk terms.
        private const int MaxTokensPerCandidate = 4;
        private const int MinCandidateLength = 8;

        // A fuzzy match is only trusted when the matching name is reasonably long...
        private const int MinMatchLength = 8;

        // ...and clearly ahead of the runner-up, so ambiguous matches are rejected rather than
        // silently assigning a release to the wrong series.
        private const double MinRunnerUpRatio = 1.5;

        private static readonly TimeSpan IndexLifetime = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(5);
        private static readonly Regex TokenRegex = new Regex(@"[\p{L}\p{N}]+", RegexOptions.Compiled, RegexMatchTimeout);

        // A trailing year in parentheses/brackets is how the library disambiguates same-named shows
        // ("Show (2005)"), not part of the title a release is named after.
        private static readonly Regex YearSuffixRegex = new Regex(@"\s*[\(\[]\s*(?:19|20)\d{2}\s*[\)\]]\s*$", RegexOptions.Compiled, RegexMatchTimeout);

        private readonly ISeriesService _seriesService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly object _lock = new object();

        private RAMDirectory _directory;
        private DirectoryReader _reader;
        private IndexSearcher _searcher;
        private DateTime _builtAt = DateTime.MinValue;

        public SeriesTitleMatcher(ISeriesService seriesService, ISceneMappingService sceneMappingService)
        {
            _seriesService = seriesService;
            _sceneMappingService = sceneMappingService;
        }

        public Series Match(string title)
        {
            return Match(title, null);
        }

        public Series Match(string title, Series restrictTo)
        {
            var candidates = BuildCandidates(title);
            var fullTitle = CompactTitle(title);

            if (candidates.Count == 0 && fullTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            var candidateSet = new HashSet<string>(candidates, StringComparer.Ordinal);

            // The index (and its DirectoryReader) is rebuilt/disposed in place; hold the lock for the
            // whole search so Invalidate/BuildIndex cannot dispose the reader out from under us.
            lock (_lock)
            {
                var searcher = GetSearcher();
                var topDocs = searcher.Search(BuildQuery(candidates, fullTitle), MaxResults);

                if (topDocs.ScoreDocs.Length == 0)
                {
                    return null;
                }

                var seriesMatches = topDocs.ScoreDocs
                    .Select(scoreDoc => ResolveSeries(searcher, scoreDoc.Doc))
                    .Where(series => series != null)
                    .GroupBy(series => series.Id)
                    .Select(group => group.First())
                    .ToList();

                // A whole-title match (ignoring a trailing year) is decisive. This resolves a release
                // named exactly after an added series ("KAKEGURUI TWIN" -> "Kakegurui Twin (2022)")
                // without dragging in the parent show, whose name is only a prefix of the parsed title.
                var titleMatches = fullTitle.IsNullOrWhiteSpace()
                    ? new List<Series>()
                    : seriesMatches.Where(series => CompactTitle(series.Title) == fullTitle).ToList();

                if (restrictTo != null)
                {
                    var restricted = titleMatches.FirstOrDefault(series => series.Id == restrictTo.Id);

                    if (restricted != null)
                    {
                        return restricted;
                    }
                }
                else if (titleMatches.Count == 1)
                {
                    return titleMatches[0];
                }

                // Lucene finds the documents that match at least one candidate term; the winner is then
                // decided in code by the length of the alias that actually matched, so the ranking does
                // not depend on Lucene's tf/idf weighting.
                var matches = seriesMatches
                    .Select(series => new { Series = series, Length = LongestMatchedName(series, candidateSet) })
                    .Where(match => match.Length >= MinMatchLength)
                    .OrderByDescending(match => match.Length)
                    .ToList();

                if (matches.Count == 0)
                {
                    return null;
                }

                var best = matches[0];

                if (restrictTo != null)
                {
                    return best.Series.Id == restrictTo.Id ? best.Series : null;
                }

                if (matches.Count > 1 && best.Length < matches[1].Length * MinRunnerUpRatio)
                {
                    return null;
                }

                return best.Series;
            }
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                DisposeIndex();
                _builtAt = DateTime.MinValue;
            }
        }

        private int LongestMatchedName(Series series, HashSet<string> candidates)
        {
            var best = 0;

            foreach (var mapping in _sceneMappingService.FindByTvdbId(series.TvdbId))
            {
                var name = Compact(mapping.Title);

                if (name != null && candidates.Contains(name))
                {
                    best = Math.Max(best, name.Length);
                }
            }

            return best;
        }

        private static BooleanQuery BuildQuery(IEnumerable<string> candidates, string fullTitle)
        {
            var query = new BooleanQuery();

            foreach (var candidate in candidates)
            {
                query.Add(new TermQuery(new Term(NameField, candidate)), Occur.SHOULD);
            }

            if (!fullTitle.IsNullOrWhiteSpace())
            {
                query.Add(new TermQuery(new Term(TitleField, fullTitle)), Occur.SHOULD);
            }

            return query;
        }

        private static List<string> BuildCandidates(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return new List<string>();
            }

            var tokens = TokenRegex.Matches(title.RemoveDiacritics().ToLowerInvariant())
                                   .Select(m => m.Value)
                                   .ToList();

            var candidates = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < tokens.Count; i++)
            {
                var builder = new StringBuilder();

                for (var j = i; j < tokens.Count && j < i + MaxTokensPerCandidate; j++)
                {
                    builder.Append(tokens[j]);

                    if (builder.Length >= MinCandidateLength)
                    {
                        candidates.Add(builder.ToString());
                    }
                }
            }

            return candidates.ToList();
        }

        private static string Compact(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            return string.Concat(TokenRegex.Matches(value.RemoveDiacritics().ToLowerInvariant())
                                           .Select(m => m.Value));
        }

        private static string CompactTitle(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            return Compact(YearSuffixRegex.Replace(value, string.Empty));
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
                    var document = new Document
                    {
                        new StringField("id", series.Id.ToString(), Field.Store.YES)
                    };

                    // The series title is indexed whole (year stripped) so an exactly-named release
                    // still resolves; the curated scene aliases (TheXEM names) additionally match as
                    // substrings of the parsed title.
                    AddName(document, TitleField, CompactTitle(series.Title));

                    foreach (var mapping in _sceneMappingService.FindByTvdbId(series.TvdbId))
                    {
                        AddName(document, NameField, Compact(mapping.Title));
                    }

                    writer.AddDocument(document);
                }
            }

            _directory = directory;
            _reader = DirectoryReader.Open(directory);
            _searcher = new IndexSearcher(_reader);
            _builtAt = DateTime.UtcNow;
        }

        private static void AddName(Document document, string field, string name)
        {
            if (!name.IsNullOrWhiteSpace())
            {
                document.Add(new StringField(field, name, Field.Store.NO));
            }
        }

        private Series ResolveSeries(IndexSearcher searcher, int docId)
        {
            var document = searcher.Doc(docId);

            if (!int.TryParse(document.Get("id"), out var seriesId))
            {
                return null;
            }

            return _seriesService.GetSeries(seriesId);
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
