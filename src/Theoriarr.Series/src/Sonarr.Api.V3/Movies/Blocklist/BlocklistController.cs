using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Movies.Blocklist
{
    [V3ApiController("blocklist")]
    [AppSubsystem(AppSubsystem.Movies)]
    public class BlocklistController : Controller
    {
        private readonly IBlocklistService _blocklistService;
        private readonly ICustomFormatCalculationService _formatCalculator;

        public BlocklistController(IBlocklistService blocklistService,
                                   ICustomFormatCalculationService formatCalculator)
        {
            _blocklistService = blocklistService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<BlocklistResource> GetBlocklist([FromQuery] PagingRequestResource paging, [FromQuery] int[] movieIds = null, [FromQuery] DownloadProtocol[] protocols = null)
        {
            var pagingResource = new PagingResource<BlocklistResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<BlocklistResource, NzbDrone.Core.Blocklisting.Blocklist>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "date",
                    "indexer",
                    "languages",
                    "movieMetadata.sortTitle",
                    "quality",
                    "sourceTitle"
                },
                "date",
                SortDirection.Descending);

            if (movieIds?.Any() == true)
            {
                pagingSpec.FilterExpressions.Add(b => movieIds.Contains(b.MovieId));
            }

            if (protocols?.Any() == true)
            {
                pagingSpec.FilterExpressions.Add(b => protocols.Contains(b.Protocol));
            }

            return pagingSpec.ApplyToPage(b => _blocklistService.Paged(pagingSpec, MediaType.Movie), b => BlocklistResourceMapper.MapToResource(b, _formatCalculator));
        }

        [HttpGet("movie")]
        public List<BlocklistResource> GetMovieBlocklist(int movieId)
        {
            return _blocklistService.GetByMovieId(movieId).Select(h => BlocklistResourceMapper.MapToResource(h, _formatCalculator)).ToList();
        }

        [RestDeleteById]
        public void DeleteBlocklist(int id)
        {
            _blocklistService.Delete(id);
        }

        [HttpDelete("bulk")]
        [Produces("application/json")]
        public object Remove([FromBody] Sonarr.Api.V3.Blocklist.BlocklistBulkResource resource)
        {
            _blocklistService.Delete(resource.Ids);

            return new { };
        }
    }
}
