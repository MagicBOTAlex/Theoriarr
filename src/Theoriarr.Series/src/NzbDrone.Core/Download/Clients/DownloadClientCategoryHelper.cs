using System;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Download.Clients
{
    internal static class DownloadClientCategoryHelper
    {
        public static bool IsCategory(string itemCategory, string category)
        {
            return category.IsNotNullOrWhiteSpace() &&
                   itemCategory.IsNotNullOrWhiteSpace() &&
                   category.Equals(itemCategory, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMovieCategory(string itemCategory, string movieCategory)
        {
            return IsCategory(itemCategory, movieCategory);
        }

        // The category only tells us the media type when the two subjects are actually labelled
        // differently. With the unified default (both "theoriarr") an item in that category is
        // ambiguous, so callers must fall back to the grab history / parsed title.
        public static bool CategoriesAreDistinct(IDownloadClientCategorySettings settings)
        {
            return settings != null &&
                   settings.MovieCategory.IsNotNullOrWhiteSpace() &&
                   !settings.MovieCategory.Equals(settings.SeriesCategory, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMovieCategory(string itemCategory, IDownloadClientCategorySettings settings)
        {
            return CategoriesAreDistinct(settings) && IsCategory(itemCategory, settings.MovieCategory);
        }

        public static bool IsSeriesCategory(string itemCategory, IDownloadClientCategorySettings settings)
        {
            return CategoriesAreDistinct(settings) && IsCategory(itemCategory, settings.SeriesCategory);
        }

        // Movies share the series category unless a separate movie category is configured. The
        // effective value is what a movie download should be labelled with (and where its
        // post-import label defaults to).
        public static string EffectiveMovieCategory(IDownloadClientCategorySettings settings)
        {
            return settings.MovieCategory.IsNotNullOrWhiteSpace() ? settings.MovieCategory : settings.SeriesCategory;
        }

        // A blank TV category preserves the historical unfiltered behaviour. A configured movie
        // category adds movie-labelled items to the tracked set without disabling TV filtering.
        public static bool MatchesCategory(string itemCategory, string tvCategory, string movieCategory)
        {
            if (tvCategory.IsNullOrWhiteSpace())
            {
                return true;
            }

            if (IsCategory(itemCategory, tvCategory))
            {
                return true;
            }

            return movieCategory.IsNotNullOrWhiteSpace() && IsCategory(itemCategory, movieCategory);
        }
    }
}
