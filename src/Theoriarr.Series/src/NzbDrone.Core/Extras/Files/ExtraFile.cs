using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Extras.Files
{
    // D3: shared-table superset. A single ExtraFile row belongs to either the SERIES or
    // MOVIES domain; the unused domain columns are NULL and MediaType records which one.
    // MIGRATION NEED: the ExtraFiles/SubtitleFiles/MetadataFiles tables currently only
    // carry the SERIES columns plus the MOVIES MovieId/MovieFileId added by
    // 265_movie_extras. They must additionally become nullable on
    // SeriesId/EpisodeFileId/SeasonNumber and add a MediaType column before this model
    // can round-trip. Not authored here (see report 09 / D3).
    public enum MediaType
    {
        Series = 0,
        Movie = 1
    }

    public abstract class ExtraFile : ModelBase
    {
        public int? SeriesId { get; set; }
        public int? EpisodeFileId { get; set; }
        public int? SeasonNumber { get; set; }
        public int? MovieId { get; set; }
        public int? MovieFileId { get; set; }
        public MediaType MediaType { get; set; }
        public string RelativePath { get; set; }
        public DateTime Added { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Extension { get; set; }

        public override string ToString()
        {
            return $"[{Id}] {RelativePath}";
        }
    }

    public enum ExtraFileType
    {
        Subtitle = 0,
        Metadata = 1,
        Other = 2
    }
}
