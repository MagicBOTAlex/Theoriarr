namespace NzbDrone.Core.MetadataSource.Provider.Resource
{
    // D7: MOVIES' multi-provider rating bag. The SERIES RatingResource (flat Count/Value)
    // is kept for Show/Episode resources; the movie shape lives here to avoid the
    // same-FQN incompatibility noted in report 07 conflict #8.
    public class MovieRatingsResource
    {
        public RatingItem Tmdb { get; set; }
        public RatingItem Imdb { get; set; }
        public RatingItem Metacritic { get; set; }
        public RatingItem RottenTomatoes { get; set; }
        public RatingItem Trakt { get; set; }
    }
}
