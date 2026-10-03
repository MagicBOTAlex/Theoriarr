using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Torznab
{
    public class TorznabRssParser : TorrentRssParser
    {
        public const string ns = "{http://torznab.com/schemas/2015/feed}";

        public TorznabRssParser()
        {
            UseEnclosureUrl = true;
        }

        protected override bool PreProcess(IndexerResponse indexerResponse)
        {
            if (indexerResponse.HttpResponse.HasHttpError &&
                (indexerResponse.HttpResponse.Headers.ContentType == null || !indexerResponse.HttpResponse.Headers.ContentType.Contains("xml")))
            {
                base.PreProcess(indexerResponse);
            }

            var xdoc = LoadXmlDocument(indexerResponse);
            var error = xdoc.Descendants("error").FirstOrDefault();

            if (error == null)
            {
                return true;
            }

            var code = Convert.ToInt32(error.Attribute("code").Value);
            var errorMessage = error.Attribute("description").Value;

            if (code >= 100 && code <= 199)
            {
                throw new ApiKeyException("Invalid API key");
            }

            if (!indexerResponse.Request.Url.FullUri.Contains("apikey=") && errorMessage == "Missing parameter")
            {
                throw new ApiKeyException("Indexer requires an API key");
            }

            if (code == 429 || errorMessage == "Request limit reached")
            {
                throw new RequestLimitReachedException("API limit reached");
            }

            if (code == 410)
            {
                throw new IndexerUnavailableException(indexerResponse, "Indexer feed is no longer available: {0}", errorMessage);
            }

            throw new TorznabException("Torznab error detected: {0}", errorMessage);
        }

        protected override bool PostProcess(IndexerResponse indexerResponse, List<XElement> items, List<ReleaseInfo> releases)
        {
            var enclosureTypes = items.SelectMany(GetEnclosures).Select(v => v.Type).Distinct().ToArray();

            if (enclosureTypes.Any() && enclosureTypes.Intersect(PreferredEnclosureMimeTypes).Empty())
            {
                if (enclosureTypes.Intersect(UsenetEnclosureMimeTypes).Any())
                {
                    _logger.Warn("{0} does not contain {1}, found {2}, did you intend to add a Newznab indexer?", indexerResponse.Request.Url, TorrentEnclosureMimeType, enclosureTypes[0]);

                    return false;
                }

                _logger.Warn("{0} does not contain {1}, found {2}.", indexerResponse.Request.Url, TorrentEnclosureMimeType, enclosureTypes[0]);
            }

            return true;
        }

        protected override ReleaseInfo ProcessItem(XElement item, ReleaseInfo releaseInfo)
        {
            var torrentInfo = base.ProcessItem(item, releaseInfo) as TorrentInfo;

            if (torrentInfo != null)
            {
                // Populate the union of TV and movie IDs so both domains can match.
                torrentInfo.TvdbId = GetTvdbId(item);
                torrentInfo.TvRageId = GetTvRageId(item);
                torrentInfo.TmdbId = GetTmdbId(item);
                releaseInfo.ImdbId = GetImdbId(item);
                torrentInfo.IndexerFlags = GetFlags(item);
            }

            return torrentInfo;
        }

        protected override string GetInfoUrl(XElement item)
        {
            return ParseUrl(item.TryGetValue("comments").TrimEnd("#comments"));
        }

        protected override string GetCommentUrl(XElement item)
        {
            return ParseUrl(item.TryGetValue("comments"));
        }

        protected override List<Language> GetLanguages(XElement item)
        {
            var languages = TryGetMultipleTorznabAttributes(item, "language");
            var results = new List<Language>();

            // Try to find <language> elements for some indexers that suck at following the rules.
            if (languages.Count == 0)
            {
                languages = item.Elements("language").Select(e => e.Value).ToList();
            }

            foreach (var language in languages)
            {
                var mappedLanguage = IsoLanguages.FindByName(language)?.Language ?? null;

                if (mappedLanguage != null)
                {
                    results.Add(mappedLanguage);
                }
            }

            return results;
        }

        protected override long GetSize(XElement item)
        {
            var sizeString = TryGetTorznabAttribute(item, "size");
            if (!sizeString.IsNullOrWhiteSpace() && long.TryParse(sizeString, out var size))
            {
                return size;
            }

            size = GetEnclosureLength(item);

            if (size <= 0)
            {
                var sizeElement = item.Element("size");

                if (sizeElement != null && long.TryParse(sizeElement.Value, out var elementSize))
                {
                    return elementSize;
                }
            }

            return size;
        }

        protected override DateTime GetPublishDate(XElement item)
        {
            return base.GetPublishDate(item);
        }

        protected override string GetDownloadUrl(XElement item)
        {
            var url = base.GetDownloadUrl(item);

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                url = ParseUrl((string)item.Element("enclosure").Attribute("url"));
            }

            return url;
        }

        protected virtual int GetTmdbId(XElement item)
        {
            var tmdbIdString = TryGetTorznabAttribute(item, "tmdbid");

            if (!tmdbIdString.IsNullOrWhiteSpace() && int.TryParse(tmdbIdString, out var tmdbId))
            {
                return tmdbId;
            }

            return 0;
        }

        protected virtual int GetTvdbId(XElement item)
        {
            var tvdbIdString = TryGetTorznabAttribute(item, "tvdbid");

            if (!tvdbIdString.IsNullOrWhiteSpace() && int.TryParse(tvdbIdString, out var tvdbId))
            {
                return tvdbId;
            }

            return 0;
        }

        protected virtual int GetTvRageId(XElement item)
        {
            var tvRageIdString = TryGetTorznabAttribute(item, "rageid");

            if (!tvRageIdString.IsNullOrWhiteSpace() && int.TryParse(tvRageIdString, out var tvRageId))
            {
                return tvRageId;
            }

            return 0;
        }

        protected virtual string GetImdbId(XElement item)
        {
            var imdbIdString = TryGetTorznabAttribute(item, "imdb");

            if (!imdbIdString.IsNullOrWhiteSpace() && int.TryParse(imdbIdString, out var imdbId) && imdbId > 0)
            {
                return $"tt{imdbId:D7}";
            }

            return null;
        }

        protected override string GetInfoHash(XElement item)
        {
            return TryGetTorznabAttribute(item, "infohash");
        }

        protected override string GetMagnetUrl(XElement item)
        {
            var magnetUrl = TryGetTorznabAttribute(item, "magneturl");

            if (magnetUrl.IsNotNullOrWhiteSpace())
            {
                return magnetUrl;
            }

            return base.GetMagnetUrl(item);
        }

        protected override int? GetSeeders(XElement item)
        {
            var seeders = TryGetTorznabAttribute(item, "seeders");

            if (seeders.IsNotNullOrWhiteSpace())
            {
                return int.Parse(seeders);
            }

            return base.GetSeeders(item);
        }

        protected override int? GetPeers(XElement item)
        {
            var peers = TryGetTorznabAttribute(item, "peers");

            if (peers.IsNotNullOrWhiteSpace())
            {
                return int.Parse(peers);
            }

            var seeders = TryGetTorznabAttribute(item, "seeders");
            var leechers = TryGetTorznabAttribute(item, "leechers");

            if (seeders.IsNotNullOrWhiteSpace() && leechers.IsNotNullOrWhiteSpace())
            {
                return int.Parse(seeders) + int.Parse(leechers);
            }

            return base.GetPeers(item);
        }

        protected IndexerFlags GetFlags(XElement item)
        {
            IndexerFlags flags = 0;

            var downloadFactor = TryGetFloatTorznabAttribute(item, "downloadvolumefactor", 1);
            var uploadFactor = TryGetFloatTorznabAttribute(item, "uploadvolumefactor", 1);

            if (downloadFactor == 0.5)
            {
                flags |= IndexerFlags.Halfleech;
            }

            if (downloadFactor == 0.75)
            {
                flags |= IndexerFlags.Freeleech25;
            }

            if (downloadFactor == 0.25)
            {
                flags |= IndexerFlags.Freeleech75;
            }

            if (downloadFactor == 0.0)
            {
                flags |= IndexerFlags.Freeleech;
            }

            if (uploadFactor == 2.0)
            {
                flags |= IndexerFlags.DoubleUpload;
            }

            if (TryGetTorznabAttribute(item, "subs").IsNotNullOrWhiteSpace())
            {
                flags |= IndexerFlags.Subtitles;
            }

            if (TryGetTorznabAttribute(item, "exclusive").IsNotNullOrWhiteSpace())
            {
                flags |= IndexerFlags.Exclusive;
            }

            if (TryGetTorznabAttribute(item, "neutralleech").IsNotNullOrWhiteSpace())
            {
                flags |= IndexerFlags.NeutralLeech;
            }

            var tags = TryGetMultipleTorznabAttributes(item, "tag");

            if (tags.Any(t => t.EqualsIgnoreCase("internal")))
            {
                flags |= IndexerFlags.Internal;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("subs")))
            {
                flags |= IndexerFlags.Subtitles;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("exclusive")))
            {
                flags |= IndexerFlags.Exclusive;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("neutralleech")))
            {
                flags |= IndexerFlags.NeutralLeech;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("scene")))
            {
                flags |= IndexerFlags.Scene;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("freeleech")))
            {
                flags |= IndexerFlags.Freeleech;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("halfleech")))
            {
                flags |= IndexerFlags.Halfleech;
            }

            if (tags.Any(t => t.EqualsIgnoreCase("doubleupload")))
            {
                flags |= IndexerFlags.DoubleUpload;
            }

            return flags;
        }

        protected string TryGetTorznabAttribute(XElement item, string key, string defaultValue = "")
        {
            var attrElement = item.Elements(ns + "attr").FirstOrDefault(e => e.Attribute("name").Value.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (attrElement != null)
            {
                var attrValue = attrElement.Attribute("value");
                if (attrValue != null)
                {
                    return attrValue.Value;
                }
            }

            return defaultValue;
        }

        protected float TryGetFloatTorznabAttribute(XElement item, string key, float defaultValue = 0)
        {
            var attr = TryGetTorznabAttribute(item, key, defaultValue.ToString());

            return float.TryParse(attr, out var result) ? result : defaultValue;
        }

        protected List<string> TryGetMultipleTorznabAttributes(XElement item, string key)
        {
            var attrElements = item.Elements(ns + "attr").Where(e => e.Attribute("name").Value.Equals(key, StringComparison.OrdinalIgnoreCase));
            var results = new List<string>();

            foreach (var element in attrElements)
            {
                var attrValue = element.Attribute("value");
                if (attrValue != null)
                {
                    results.Add(attrValue.Value);
                }
            }

            return results;
        }
    }
}
