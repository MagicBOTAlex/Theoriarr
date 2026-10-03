using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications;
public static class NotificationMetadataLinkGenerator
{
    public static List<NotificationMetadataLink> GenerateLinks(Series series, IEnumerable<int> metadataLinks)
    {
        var links = new List<NotificationMetadataLink>();

        if (series == null)
        {
            return links;
        }

        foreach (var link in metadataLinks)
        {
            var linkType = (MetadataLinkType)link;

            if (linkType == MetadataLinkType.Imdb && series.ImdbId.IsNotNullOrWhiteSpace())
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Imdb, "IMDb", $"https://www.imdb.com/title/{series.ImdbId}"));
            }

            if (linkType == MetadataLinkType.Tvdb && series.TvdbId > 0)
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Tvdb, "TVDb", $"http://www.thetvdb.com/?tab=series&id={series.TvdbId}"));
            }

            if (linkType == MetadataLinkType.Trakt && series.TvdbId > 0)
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Trakt, "Trakt", $"http://trakt.tv/search/tvdb/{series.TvdbId}?id_type=show"));
            }

            if (linkType == MetadataLinkType.Tvmaze && series.TvMazeId > 0)
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Tvmaze, "TVMaze", $"http://www.tvmaze.com/shows/{series.TvMazeId}/_"));
            }
        }

        return links;
    }

    public static List<NotificationMetadataLink> GenerateLinks(Movie movie, IEnumerable<int> metadataLinks)
    {
        var links = new List<NotificationMetadataLink>();

        if (movie == null)
        {
            return links;
        }

        foreach (var type in metadataLinks)
        {
            var linkType = (MetadataLinkType)type;

            if (linkType == MetadataLinkType.Imdb && movie.ImdbId.IsNotNullOrWhiteSpace())
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Imdb, "IMDb", $"https://www.imdb.com/title/{movie.ImdbId}"));
            }

            if (linkType == MetadataLinkType.Tmdb && movie.TmdbId > 0)
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Tmdb, "TMDb", $"https://www.themoviedb.org/movie/{movie.TmdbId}"));
            }

            if (linkType == MetadataLinkType.Trakt && movie.TmdbId > 0)
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Trakt, "Trakt", $"https://trakt.tv/search/tmdb/{movie.TmdbId}?id_type=movie"));
            }
        }

        return links;
    }
}
