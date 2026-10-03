using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.ImportLists;

namespace Sonarr.Http.Subsystem
{
    // API-layer domain scoping for the shared (dual-tagged) stores.
    //
    // The D3 merge shares one table for RootFolders, Tags and QualityProfiles. Each now
    // carries a MediaType discriminator (RootFolders/Tags via 315, QualityProfiles via
    // 311), but legacy rows all default to Series(0), so the stored value alone cannot be
    // trusted to mean "series only": treating 0 as series-only would empty the movie
    // domain. The helper therefore combines the discriminator with *usage* (which
    // subsystem actually references the item).
    //
    // Chosen semantics (audit B2):
    //  - explicit MediaType.Movie   -> movies only, never visible to the series key.
    //  - explicit MediaType.Anime   -> series domain only (anime is a Sonarr SeriesType), so
    //    never visible to the movie key. The absolute path is still served by the series API.
    //  - MediaType.Series/shared(0) -> always visible to the series key. The legacy
    //    default cannot be told apart from a genuinely series-only row, and hiding it
    //    blocks series setup (AC1).
    //  - MediaType.Series/shared(0) -> visible to movies unless it is referenced only by
    //    series (AC5), in which case the movie list endpoint falls back to the unfiltered
    //    rows via WithSetupFallback so the list is never empty (AC2).
    public static class SubsystemDomainScope
    {
        public static MediaType ToMediaType(this AppSubsystem subsystem)
        {
            return subsystem == AppSubsystem.Movies ? MediaType.Movie : MediaType.Series;
        }

        public static bool IsVisibleTo(AppSubsystem requested, bool usedBySeries, bool usedByMovies)
        {
            // The series key is never refused a non-Movie row; the movie key omits only
            // rows referenced exclusively by series.
            return requested == AppSubsystem.Series || !usedBySeries || usedByMovies;
        }

        public static bool IsVisibleTo(AppSubsystem requested, MediaType mediaType, bool usedBySeries, bool usedByMovies)
        {
            if (mediaType == MediaType.Movie)
            {
                return requested == AppSubsystem.Movies;
            }

            if (mediaType == MediaType.Anime)
            {
                return requested == AppSubsystem.Series;
            }

            return IsVisibleTo(requested, usedBySeries, usedByMovies);
        }

        // List endpoints apply the usage filter and then pass the unfiltered and filtered
        // collections here. IsVisibleTo never empties the series list, so the fallback
        // effectively only triggers for the movie key: if narrowing hid every row (for
        // example the only seeded quality profile is already referenced by a series),
        // return the unfiltered rows so Jellyseerr can still configure the connection.
        // When a movie-visible alternative exists the filtered list is non-empty and is
        // returned unchanged, preserving the 4.3 intent (AC5).
        public static IEnumerable<T> WithSetupFallback<T>(AppSubsystem requested, IEnumerable<T> all, IReadOnlyCollection<T> visible)
        {
            if (requested == AppSubsystem.Movies && visible.Count == 0)
            {
                return all;
            }

            return visible;
        }

        public static bool IsPathUnder(this string rootFolderPath, IEnumerable<string> paths)
        {
            if (rootFolderPath.IsNullOrWhiteSpace() || paths == null)
            {
                return false;
            }

            return paths.Any(path => path.IsNotNullOrWhiteSpace() &&
                                     (rootFolderPath.PathEquals(path) || rootFolderPath.IsParentPath(path)));
        }
    }
}
