using NzbDrone.Core.Movies;

namespace NzbDrone.Core.MediaFiles
{
    public class MovieFile : MediaFileBase
    {
        public int MovieId { get; set; }
        public string Edition { get; set; }
        public Movie Movie { get; set; }
    }
}
