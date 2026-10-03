using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Queue
{
    public static class QueueExtensions
    {
        public static IEnumerable<Queue> SeriesItems(this IEnumerable<Queue> queue, bool includeUnknown)
        {
            return includeUnknown ? queue.Where(q => q.Movie == null) : queue.Where(q => q.Series != null);
        }

        public static IEnumerable<Queue> MovieItems(this IEnumerable<Queue> queue, bool includeUnknown)
        {
            return includeUnknown ? queue.Where(q => q.Series == null) : queue.Where(q => q.Movie != null);
        }
    }
}
