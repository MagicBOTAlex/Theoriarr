namespace NzbDrone.Core.Download.Clients
{
    // Theoriarr is one application talking to one download client, so the default category is a
    // single shared label rather than the old Sonarr/Radarr pair ("tv-sonarr"/"radarr"). Both the
    // series and movie category fields default to this value; the media type of a download is
    // resolved from its grab history (or parsed title), not from the category.
    public static class DownloadClientSettingsDefaults
    {
        public const string Category = "theoriarr";
    }
}
