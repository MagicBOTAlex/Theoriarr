using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public class EpisodeFile : MediaFileBase
    {
        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public string ReleaseHash { get; set; }
        public LazyLoaded<List<Episode>> Episodes { get; set; }
        public LazyLoaded<Series> Series { get; set; }
        public ReleaseType ReleaseType { get; set; }
    }
}
