using System.Collections.Generic;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Library;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Library
{
    public class LibrarySearchResource : RestResource
    {
        public string MediaType { get; set; }
    }

    public static class LibrarySearchResourceMapper
    {
        public static List<LibrarySearchResource> ToResource(this List<LibrarySearchResult> results)
        {
            return results.ConvertAll(result => new LibrarySearchResource
            {
                MediaType = result.MediaType == MediaType.Movie ? "movie" : "series",
                Id = result.Id
            });
        }
    }
}
