using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.MediaCovers;

[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
public class MediaCoverController : Controller
{
    private static readonly Regex RegexResizedImage = new(@"-\d+\.jpg$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
    [Produces("image/jpeg", "image/png", "image/gif")]
    public Results<PhysicalFileHttpResult, NotFound> GetMediaCover(int seriesId, string filename)
    {
        return GetMediaCoverFile(seriesId.ToString(), filename);
    }

    private Results<PhysicalFileHttpResult, NotFound> GetMediaCoverFile(string relativeFolder, string filename)
    {
        filename = Path.GetFileName(filename);

        if (filename.IsNullOrWhiteSpace())
        {
            return TypedResults.NotFound();
        }

        var root = Path.GetFullPath(_appFolderInfo.GetAppDataPath());
        var filePath = Path.GetFullPath(Path.Combine(root, "MediaCover", relativeFolder, filename));

        // Defence in depth: never serve anything that escaped the media-cover root even if a
        // traversal sequence slipped past the route constraint or filename sanitising.
        if (!filePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
        {
            var baseFilePath = RegexResizedImage.Replace(filePath, ".jpg");

            if (baseFilePath == filePath || !_diskProvider.FileExists(baseFilePath))
            {
                return TypedResults.NotFound();
            }

            filePath = baseFilePath;
        }

        return TypedResults.PhysicalFile(filePath, GetContentType(filePath));
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
