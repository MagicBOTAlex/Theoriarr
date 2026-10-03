using System;
using System.Collections.Generic;

namespace Sonarr.Http.Subsystem
{
    // Per-domain field visibility for shared provider stores that carry both series-only and
    // movie-only settings on one row (metadata, indexers, notifications). Mirrors the
    // download-client pattern: the series key hides movie-only fields and the movies key hides
    // series-only fields. API field names and response shapes are unchanged; only the schema
    // returned for a given key is scoped.
    public static class ProviderFieldScoping
    {
        public static bool IsFieldVisible(string fieldName, AppSubsystem subsystem, ISet<string> seriesOnlyFields, ISet<string> movieOnlyFields)
        {
            if (subsystem == AppSubsystem.Series)
            {
                return !movieOnlyFields.Contains(fieldName);
            }

            if (subsystem == AppSubsystem.Movies)
            {
                return !seriesOnlyFields.Contains(fieldName);
            }

            return true;
        }

        public static class MetadataFields
        {
            public static readonly ISet<string> SeriesOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "seriesMetadata",
                "seriesMetadataEpisodeGuide",
                "seriesMetadataUrl",
                "episodeMetadata",
                "episodeImageThumb",
                "seriesImages",
                "seasonImages",
                "episodeImages",
                "seriesPlexMatchFile",
                "episodeMappings"
            };

            public static readonly ISet<string> MovieOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "movieMetadata",
                "useMovieNfo",
                "movieMetadataLanguage",
                "movieMetadataURL",
                "addCollectionName",
                "movieImages"
            };
        }

        public static class IndexerFields
        {
            public static readonly ISet<string> SeriesOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "animeCategories",
                "animeStandardFormatSearch"
            };

            public static readonly ISet<string> MovieOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "movieCategories"
            };
        }

        public static class NotificationFields
        {
            public static readonly ISet<string> SeriesOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "includeSeriesPoster"
            };

            public static readonly ISet<string> MovieOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "includeMoviePoster"
            };
        }
    }
}
