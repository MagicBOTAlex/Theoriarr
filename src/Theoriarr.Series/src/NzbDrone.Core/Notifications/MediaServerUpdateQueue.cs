using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class MediaServerUpdateQueue<TQueueHost, TItemInfo>
        where TQueueHost : class
    {
        private class UpdateQueue
        {
            public Dictionary<int, UpdateQueueItem<TItemInfo>> Pending { get; } = new Dictionary<int, UpdateQueueItem<TItemInfo>>();
            public bool Refreshing { get; set; }
        }

        private readonly ICached<UpdateQueue> _pendingMediaCache;

        public MediaServerUpdateQueue(ICacheManager cacheManager)
        {
            _pendingMediaCache = cacheManager.GetRollingCache<UpdateQueue>(typeof(TQueueHost), "pendingMedia", TimeSpan.FromDays(1));
        }

        public void Add(string identifier, Series series, TItemInfo info)
        {
            Add(identifier, series.Id, new UpdateQueueItem<TItemInfo>(series), info);
        }

        public void Add(string identifier, Movie movie, TItemInfo info)
        {
            Add(identifier, movie.Id, new UpdateQueueItem<TItemInfo>(movie), info);
        }

        private void Add(string identifier, int mediaId, UpdateQueueItem<TItemInfo> candidate, TItemInfo info)
        {
            var queue = _pendingMediaCache.Get(identifier, () => new UpdateQueue());

            lock (queue)
            {
                var item = queue.Pending.TryGetValue(mediaId, out var value)
                    ? value
                    : candidate;

                item.Info.Add(info);

                queue.Pending[mediaId] = item;
            }
        }

        public void ProcessQueue(string identifier, Action<List<UpdateQueueItem<TItemInfo>>> update)
        {
            var queue = _pendingMediaCache.Find(identifier);

            if (queue == null)
            {
                return;
            }

            lock (queue)
            {
                if (queue.Refreshing)
                {
                    return;
                }

                queue.Refreshing = true;
            }

            try
            {
                while (true)
                {
                    List<UpdateQueueItem<TItemInfo>> items;

                    lock (queue)
                    {
                        if (queue.Pending.Empty())
                        {
                            queue.Refreshing = false;
                            return;
                        }

                        items = queue.Pending.Values.ToList();
                        queue.Pending.Clear();
                    }

                    update(items);
                }
            }
            catch
            {
                lock (queue)
                {
                    queue.Refreshing = false;
                }

                throw;
            }
        }
    }

    public class UpdateQueueItem<TItemInfo>
    {
        public int MediaId { get; set; }
        public Series Series { get; set; }
        public Movie Movie { get; set; }
        public HashSet<TItemInfo> Info { get; set; }

        public UpdateQueueItem(int mediaId)
        {
            MediaId = mediaId;
            Info = new HashSet<TItemInfo>();
        }

        public UpdateQueueItem(Series series)
            : this(series.Id)
        {
            Series = series;
        }

        public UpdateQueueItem(Movie movie)
            : this(movie.Id)
        {
            Movie = movie;
        }
    }
}
