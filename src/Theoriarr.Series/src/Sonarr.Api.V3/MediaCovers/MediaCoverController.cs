using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using Sonarr.Http;
using Sonarr.Http.Frontend.Mappers;

namespace Sonarr.Api.V3.MediaCovers
{
    [V3ApiController]
    public class MediaCoverController : Controller
    {
        private static readonly Regex RegexResizedImage = new Regex(@"-\d+\.jpg$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IContentTypeProvider _mimeTypeProvider;

        public MediaCoverController(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _mimeTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet(@"{seriesId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetMediaCover(int seriesId, string filename)
        {
            filename = Path.GetFileName(filename);

            if (filename.IsNullOrWhiteSpace())
            {
                return NotFound();
            }

            return GetMediaCoverFile(MediaCoverRoute.GetSeriesPath(seriesId, filename));
        }

        [HttpGet(@"movie/{movieId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetMovieMediaCover(int movieId, string filename)
        {
            filename = Path.GetFileName(filename);

            if (filename.IsNullOrWhiteSpace())
            {
                return NotFound();
            }

            return GetMediaCoverFile(MediaCoverRoute.GetMoviePath(movieId, filename));
        }

        private IActionResult GetMediaCoverFile(string relativePath)
        {
            var root = Path.GetFullPath(_appFolderInfo.GetAppDataPath());
            var filePath = Path.GetFullPath(Path.Combine(root, relativePath));

            // Defence in depth: never serve anything that escaped the media-cover root even if
            // a traversal sequence slipped past the route constraint or filename sanitising.
            if (!filePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return NotFound();
            }

            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, ".jpg");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return NotFound();
                }

                filePath = basefilePath;
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }

        private string GetContentType(string filePath)
        {
            if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
