namespace NzbDrone.Core.Qualities
{
    // Canonical union of the SERIES (Sonarr) and MOVIES (Radarr) QualitySource enums.
    // QualitySource is not persisted (qualities persist by numeric Id via QualityIntConverter),
    // so this union is a compile-time / mapping concern only.
    //
    // The SERIES names and values are canonical. MOVIES-only names are declared as aliases
    // of the matching SERIES member (identical numeric value) so MOVIES code compiles without
    // changing meaning. The MOVIES pre-release sources have no SERIES equivalent and get new
    // values that cannot collide with the canonical SERIES members.
    public enum QualitySource
    {
        Unknown = 0,
        Television = 1,
        TelevisionRaw = 2,
        Web = 3,
        WebRip = 4,
        DVD = 5,
        Bluray = 6,
        BlurayRaw = 7,

        // MOVIES aliases (same value as their canonical SERIES equivalent).
        UNKNOWN = Unknown,
        TV = Television,
        WEBDL = Web,
        WEBRIP = WebRip,
        BLURAY = Bluray,

        // MOVIES-only pre-release sources (new values, no SERIES equivalent).
        CAM = 8,
        TELESYNC = 9,
        TELECINE = 10,
        WORKPRINT = 11
    }
}
