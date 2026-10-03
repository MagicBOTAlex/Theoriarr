using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.Download.Clients
{
    public static class DownloadClientMediaTypeHelper
    {
        public static bool SupportsMediaType(IDownloadClientMediaTypeSettings settings, MediaType mediaType)
        {
            if (settings == null)
            {
                return true;
            }

            return mediaType switch
            {
                MediaType.Movie => settings.DownloadMovies,
                MediaType.Anime => settings.DownloadAnime,
                _ => settings.DownloadSeries
            };
        }

        public static bool SupportsMediaType(this IDownloadClient client, MediaType? mediaType)
        {
            if (!mediaType.HasValue)
            {
                return true;
            }

            return SupportsMediaType(client.Definition.Settings as IDownloadClientMediaTypeSettings, mediaType.Value);
        }
    }
}
