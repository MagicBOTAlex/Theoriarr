using System.Collections.Generic;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class DownloadMessage
    {
        public string Message { get; set; }
        public Series Series { get; set; }
        public LocalEpisode EpisodeInfo { get; set; }
        public EpisodeFile EpisodeFile { get; set; }
        public List<DeletedEpisodeFile> OldFiles { get; set; }
        public Movie Movie { get; set; }
        public LocalMovie MovieInfo { get; set; }
        public MovieFile MovieFile { get; set; }
        public List<DeletedMovieFile> OldMovieFiles { get; set; }
        public string SourcePath { get; set; }
        public DownloadClientItemClientInfo DownloadClientInfo { get; set; }
        public string DownloadId { get; set; }
        public GrabbedReleaseInfo Release { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
