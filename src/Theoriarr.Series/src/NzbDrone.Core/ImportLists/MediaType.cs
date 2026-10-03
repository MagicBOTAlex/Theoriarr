namespace NzbDrone.Core.ImportLists
{
    // D5: media discriminator for the two import-list item pipelines.
    // SERIES providers emit ImportListItemInfo, MOVIES providers emit ImportListMovie.
    // Values are intentionally NOT persisted on their own; where a media type is stored
    // (ImportListDefinition/SyncCommand) it is a new nullable-friendly discriminator.
    //
    // Anime is a first-class type of the SERIES domain (it is a Sonarr SeriesTypes.Anime
    // series), so the series API key owns both Series(0)/"TV" and Anime(2) rows while the
    // movie key only ever sees Movie(1). It is used to tag root folders.
    public enum MediaType
    {
        Series = 0,
        Movie = 1,
        Anime = 2
    }
}
