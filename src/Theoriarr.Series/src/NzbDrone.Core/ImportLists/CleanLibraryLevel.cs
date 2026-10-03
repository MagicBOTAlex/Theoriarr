namespace NzbDrone.Core.ImportLists
{
    // MOVIES enum kept for Radarr V3 API compatibility (strings "disabled"/"logOnly"/
    // "keepAndUnmonitor"/"removeAndKeep"/"removeAndDelete"). The unified config uses
    // ListSyncLevelType; map between them where the movie API is served.
    public enum CleanLibraryLevel
    {
        Disabled = 0,
        LogOnly = 1,
        KeepAndUnmonitor = 2,
        RemoveAndKeep = 3,
        RemoveAndDelete = 4
    }
}
