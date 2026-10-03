namespace NzbDrone.Core.Extras.Metadata
{
    // D7 / report 07 §2.5: SERIES owns 1-5; MOVIES types are renumbered to 6-7 so the
    // shared MetadataFiles.Type column never collides. MIGRATION NEED: existing MOVIES
    // rows persisted Type 1 (MovieMetadata) / 2 (MovieImage); a data migration must remap
    // 1->6 and 2->7 before the movies domain is enabled. Not authored here.
    public enum MetadataType
    {
        Unknown = 0,
        SeriesMetadata = 1,
        EpisodeMetadata = 2,
        SeriesImage = 3,
        SeasonImage = 4,
        EpisodeImage = 5,
        MovieMetadata = 6,
        MovieImage = 7
    }
}
