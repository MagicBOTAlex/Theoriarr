using System;
using System.IO;

namespace Sonarr.Http.Frontend.Mappers
{
    public static class MediaCoverRoute
    {
        public const string RootSegment = "MediaCover";
        public const string MovieSegment = "movie";
        public const string RootPrefix = "/MediaCover/";
        public const string MoviePrefix = "/MediaCover/movie/";

        public static bool IsMediaCover(string resourceUrl)
        {
            return resourceUrl != null &&
                   resourceUrl.StartsWith(RootPrefix, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsMovie(string resourceUrl)
        {
            return resourceUrl != null &&
                   resourceUrl.StartsWith(MoviePrefix, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsSeries(string resourceUrl)
        {
            return IsMediaCover(resourceUrl) && !IsMovie(resourceUrl);
        }

        public static string GetSeriesPath(int seriesId, string filename)
        {
            return Path.Combine(RootSegment, seriesId.ToString(), filename);
        }

        public static string GetMoviePath(int movieId, string filename)
        {
            return Path.Combine(RootSegment, MovieSegment, movieId.ToString(), filename);
        }
    }
}
