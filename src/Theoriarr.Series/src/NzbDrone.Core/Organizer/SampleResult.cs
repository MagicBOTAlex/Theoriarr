using System.Collections.Generic;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Organizer
{
    public class SampleResult
    {
        public string FileName { get; set; }

        // Series (SERIES canonical)
        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }
        public EpisodeFile EpisodeFile { get; set; }

        // Movies (MOVIES union)
        public Movie Movie { get; set; }
        public MovieFile MovieFile { get; set; }
    }
}
