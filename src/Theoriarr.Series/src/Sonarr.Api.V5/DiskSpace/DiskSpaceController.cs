using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DiskSpace;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.DiskSpace;

[V5ApiController("diskspace")]
[AppSubsystem(AppSubsystem.Series)]
public class DiskSpaceController : Controller
{
    private readonly IDiskSpaceService _diskSpaceService;

    public DiskSpaceController(IDiskSpaceService diskSpaceService)
    {
        _diskSpaceService = diskSpaceService;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<DiskSpaceResource>> GetFreeSpace()
    {
        return TypedResults.Ok(_diskSpaceService.GetFreeSpace().ConvertAll(DiskSpaceResourceMapper.MapToResource));
    }

    [HttpGet("content")]
    [Produces("application/json")]
    public Ok<List<DiskSpaceContentResource>> GetContent(string? path)
    {
        return TypedResults.Ok(_diskSpaceService.GetContent(path).ConvertAll(DiskSpaceContentResourceMapper.MapToResource));
    }
}
