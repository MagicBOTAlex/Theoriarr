namespace NzbDrone.Core.ImportLists
{
    // Canonical list-sync/clean level (SERIES enum). MOVIES-only remove levels are appended
    // so the movie CleanLibrary path can switch on enum members instead of "disabled" strings.
    // Values are persisted in config; existing SERIES values are preserved.
    public enum ListSyncLevelType
    {
        Disabled = 0,
        LogOnly = 1,
        KeepAndUnmonitor = 2,
        KeepAndTag = 3,
        RemoveAndKeep = 4,
        RemoveAndDelete = 5
    }
}
