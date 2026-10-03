using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Others
{
    public interface IOtherExtraFileService : IExtraFileService<OtherExtraFile>
    {
    }

    public class OtherExtraFileService : ExtraFileService<OtherExtraFile>, IOtherExtraFileService
    {
        public OtherExtraFileService(IExtraFileRepository<OtherExtraFile> repository,
                                     ISeriesService seriesService,
                                     IMovieService movieService,
                                     IDiskProvider diskProvider,
                                     IRecycleBinProvider recycleBinProvider,
                                     Logger logger)
            : base(repository, seriesService, movieService, diskProvider, recycleBinProvider, logger)
        {
        }
    }
}
