namespace NzbDrone.Core.Download.Clients
{
    // Implemented by download-client settings that carry categories. `MovieCategory` already
    // exists on IMovieDownloadClientSettings; `SeriesCategory` maps to whichever property holds
    // the series category for that client (usually TvCategory, but `Category` for Hadouken and
    // FreeboxDownload). It is an interface-only member and is never serialized into the settings
    // schema, so the Sonarr/Radarr-compatible field names (tvCategory/movieCategory) are
    // unchanged.
    //
    // The two categories are only treated as a routing signal when they are both configured and
    // different. With the unified default (both "theoriarr") the category is not authoritative and
    // the media type comes from the grab history (or title parsing for manually added downloads).
    public interface IDownloadClientCategorySettings
    {
        string SeriesCategory { get; }
        string MovieCategory { get; }
    }
}
