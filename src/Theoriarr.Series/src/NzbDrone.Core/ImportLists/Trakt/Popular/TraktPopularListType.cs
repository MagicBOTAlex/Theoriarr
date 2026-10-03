using System;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.ImportLists.Trakt.Popular
{
    // D5 enum collision. SERIES ordering is canonical (Trending=0..RecommendedByAllTime=10).
    // MOVIES inserts BoxOffice at 3 and shifts the rest by one. Values are persisted and
    // surfaced in the UI, so canonical = SERIES and the movie-only member is appended.
    // D9 must remap stored MOVIES values (owned elsewhere; no migration authored here):
    //   MOVIES 3->11, 4->3, 5->4, 6->5, 7->6, 8->7, 9->8, 10->9, 11->10.
    public enum TraktPopularListType
    {
        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTrendingShows")]
        Trending = 0,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypePopularShows")]
        Popular = 1,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeAnticipatedShows")]
        Anticipated = 2,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTopWeekShows")]
        TopWatchedByWeek = 3,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTopMonthShows")]
        TopWatchedByMonth = 4,

        [Obsolete]
        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTopYearShows")]
        TopWatchedByYear = 5,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTopAllTimeShows")]
        TopWatchedByAllTime = 6,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeRecommendedWeekShows")]
        RecommendedByWeek = 7,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeRecommendedMonthShows")]
        RecommendedByMonth = 8,

        [Obsolete]
        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeRecommendedYearShows")]
        RecommendedByYear = 9,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeRecommendedAllTimeShows")]
        RecommendedByAllTime = 10,

        [FieldOption(Label = "ImportListsTraktSettingsPopularListTypeTopBoxOfficeMovies")]
        BoxOffice = 11
    }
}
