namespace NzbDrone.Core.Configuration
{
    public enum ImportConflictResolution
    {
        // Leave the download blocked for manual import and stop auto-retrying.
        Manual,

        // Recycle/overwrite the conflicting destination file and complete the import.
        Overwrite
    }
}
