import Episode from 'Episode/Episode';
import { Movie } from 'Movies/Movie';

export type MediaWantedType = 'series' | 'movie';

// One wanted/cutoff row, normalised from an episode or a movie so the unified
// Wanted page can render and sort both in a single table.
export interface MediaWantedItem {
  key: string;
  type: MediaWantedType;
  id: number;
  title: string;
  subtitle: string;
  date?: string;
  monitored: boolean;
  hasFile: boolean;
  status: string;
  link: string;
  episode?: Episode;
  movie?: Movie;
}

function formatEpisodeNumber(seasonNumber: number, episodeNumber: number) {
  return `S${String(seasonNumber).padStart(2, '0')}E${String(
    episodeNumber
  ).padStart(2, '0')}`;
}

export function episodeToMediaWantedItem(episode: Episode): MediaWantedItem {
  const series = episode.series;
  const number = formatEpisodeNumber(
    episode.seasonNumber,
    episode.episodeNumber
  );

  return {
    key: `series-${episode.id}`,
    type: 'series',
    id: episode.id,
    title: series?.title ?? 'Unknown series',
    subtitle: `${number} \u00b7 ${episode.title}`,
    date: episode.airDateUtc,
    monitored: episode.monitored,
    hasFile: episode.hasFile,
    status: episode.hasFile ? 'Downloaded' : 'Missing',
    link: series?.titleSlug ? `/series/${series.titleSlug}` : '/library',
    episode,
  };
}

export function movieToMediaWantedItem(movie: Movie): MediaWantedItem {
  return {
    key: `movie-${movie.id}`,
    type: 'movie',
    id: movie.id,
    title: movie.title,
    subtitle: String(movie.year ?? ''),
    date:
      movie.digitalRelease ||
      movie.physicalRelease ||
      movie.inCinemas ||
      movie.releaseDate,
    monitored: movie.monitored,
    hasFile: movie.hasFile,
    status: movie.status,
    link: `/movie/${movie.id}`,
    movie,
  };
}
