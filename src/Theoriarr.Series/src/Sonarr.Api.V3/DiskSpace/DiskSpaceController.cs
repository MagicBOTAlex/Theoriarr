using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DiskSpace;
using Sonarr.Http;

namespace Sonarr.Api.V3.DiskSpace
{
    [V3ApiController("diskspace")]
    public class DiskSpaceController : Controller
    {
        private readonly IDiskSpaceService _diskSpaceService;

        public DiskSpaceController(IDiskSpaceService diskSpaceService)
        {
            _diskSpaceService = diskSpaceService;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<DiskSpaceResource> GetFreeSpace()
        {
            return _diskSpaceService.GetFreeSpace().ConvertAll(DiskSpaceResourceMapper.MapToResource);
        }

        [HttpGet("content")]
        [Produces("application/json")]
        public List<DiskSpaceContentResource> GetContent(string path)
        {
            return _diskSpaceService.GetContent(path).ConvertAll(DiskSpaceContentResourceMapper.MapToResource);
        }
    }
}
