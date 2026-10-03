using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.ImportLists.Exclusions
{
    // D6 — the single, MediaType-tagged superset exclusion store. It merges the former SERIES
    // `Exclusions.ImportListExclusion` (TvdbId/Title) and the MOVIES
    // `ImportExclusions.ImportListMovieExclusion` (TmdbId/MovieTitle/MovieYear) into one table
    // (`ImportListExclusions`, migration 316). A series row uses TvdbId/Title; a movie row uses
    // TmdbId/Title (the movie title) /MovieYear.
    public class ImportListExclusion : ModelBase
    {
        public MediaType MediaType { get; set; }
        public int TvdbId { get; set; }
        public int TmdbId { get; set; }
        public string Title { get; set; }
        public int MovieYear { get; set; }

        public override string ToString()
        {
            return MediaType == MediaType.Movie
                ? string.Format("Excluded Movie: [{0}][{1} {2}]", TmdbId, Title, MovieYear)
                : string.Format("Excluded Series: [{0}][{1}]", TvdbId, Title);
        }
    }
}
