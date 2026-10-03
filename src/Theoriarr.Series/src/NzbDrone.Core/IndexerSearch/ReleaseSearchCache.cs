using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.IndexerSearch
{
    public interface IReleaseSearchCache
    {
        void Store(string cacheKey, List<DownloadDecision> decisions);
        ReleaseSearchCacheItem Get(string cacheKey);
        void Remove(string cacheKey);
    }

    // Holds the most recent completed interactive search per target (series/season/episode/movie)
    // so the client can offer to reuse it instead of hitting the indexers again. Unlike the
    // in-flight tracker this is keyed by the search target, not the ephemeral client search id.
    public class ReleaseSearchCache : IReleaseSearchCache
    {
        public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, ReleaseSearchCacheItem> _cache = new ConcurrentDictionary<string, ReleaseSearchCacheItem>(StringComparer.Ordinal);

        public void Store(string cacheKey, List<DownloadDecision> decisions)
        {
            if (cacheKey.IsNullOrWhiteSpace() || decisions == null)
            {
                return;
            }

            _cache[cacheKey] = new ReleaseSearchCacheItem
            {
                Decisions = decisions,
                CachedAt = DateTime.UtcNow
            };
        }

        public ReleaseSearchCacheItem Get(string cacheKey)
        {
            if (cacheKey.IsNullOrWhiteSpace() || !_cache.TryGetValue(cacheKey, out var item))
            {
                return null;
            }

            if (item.CachedAt < DateTime.UtcNow - Expiry)
            {
                _cache.TryRemove(cacheKey, out _);
                return null;
            }

            return item;
        }

        public void Remove(string cacheKey)
        {
            if (cacheKey.IsNotNullOrWhiteSpace())
            {
                _cache.TryRemove(cacheKey, out _);
            }
        }
    }

    public class ReleaseSearchCacheItem
    {
        public List<DownloadDecision> Decisions { get; set; }
        public DateTime CachedAt { get; set; }
    }
}
