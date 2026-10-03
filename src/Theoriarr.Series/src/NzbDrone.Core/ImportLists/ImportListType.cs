namespace NzbDrone.Core.ImportLists
{
    // D5 enum collision. SERIES: Program=0,Plex=1,Trakt=2,Simkl=3,Other=4,Advanced=5
    // MOVIES:        Program=0,TMDB=1,Trakt=2,Plex=3,Simkl=4,Other=5,Advanced=6
    // The integer is persisted (ListOrder = (int)ListType) and surfaced in both APIs, so the
    // values cannot be naively reordered. Canonical = SERIES ordering; the movie-only TMDB
    // value is appended. TMDB collides with SERIES Plex in the raw integer space, so a D9
    // data migration (owned elsewhere) must remap stored MOVIES values:
    //   MOVIES 1 (TMDB) -> 6 ; MOVIES 3 (Plex) -> 1 ; MOVIES 4 (Simkl) -> 3 ;
    //   MOVIES 5 (Other) -> 4 ; MOVIES 6 (Advanced) -> 5.
    // NO migration is authored here.
    public enum ImportListType
    {
        Program = 0,
        Plex = 1,
        Trakt = 2,
        Simkl = 3,
        Other = 4,
        Advanced = 5,
        TMDB = 6
    }
}
