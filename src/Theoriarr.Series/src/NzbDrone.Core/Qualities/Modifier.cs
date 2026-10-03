namespace NzbDrone.Core.Qualities
{
    // MOVIES (Radarr) quality modifier. Retained as orthogonal, non-persisted metadata on
    // Quality for Radarr wire compatibility. It is NOT part of Quality identity/equality
    // (Quality.Equals still compares Id only) and SERIES qualities default to NONE.
    public enum Modifier
    {
        NONE = 0,
        REGIONAL,
        SCREENER,
        RAWHD,
        BRDISK,
        REMUX
    }
}
