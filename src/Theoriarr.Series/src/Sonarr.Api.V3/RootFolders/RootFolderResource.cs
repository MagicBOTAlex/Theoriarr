using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.RootFolders;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.RootFolders
{
    public class RootFolderResource : RestResource
    {
        public string Path { get; set; }

        // Optional on write: omitted -> stamped from the API key's domain. On read it is
        // always present ("series"/"movie"/"anime"). The unified settings UI sends it so a
        // single page can create TV, Movies and Anime folders.
        public MediaType? MediaType { get; set; }

        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }

    public static class RootFolderResourceMapper
    {
        public static RootFolderResource ToResource(this RootFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new RootFolderResource
            {
                Id = model.Id,

                Path = model.Path.GetCleanPath(),
                MediaType = model.MediaType,
                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace,
                UnmappedFolders = model.UnmappedFolders
            };
        }

        public static RootFolder ToModel(this RootFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new RootFolder
            {
                Id = resource.Id,

                Path = resource.Path

                // MediaType is stamped by the controller (explicit value or API-key domain)
                // Accessible
                // FreeSpace
                // UnmappedFolders
            };
        }

        public static List<RootFolderResource> ToResource(this IEnumerable<RootFolder> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
