namespace NzbDrone.Core.Download.Clients
{
    // Per-client media-type opt-out. The unified app can talk to a single download client that
    // serves any mix of series/movies/anime, so each client can be limited to the types it should
    // receive. Every type is enabled by default; settings that do not implement this interface
    // accept every type.
    public interface IDownloadClientMediaTypeSettings
    {
        bool DownloadSeries { get; }
        bool DownloadMovies { get; }
        bool DownloadAnime { get; }
    }
}
