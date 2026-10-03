import { Movie } from 'Movies/Movie';
import Series from 'Series/Series';
import { ServiceId } from 'Services';
import getMediaCoverUrl from 'Utilities/MediaCover';

export type MediaType = 'series' | 'movie';

export interface MediaImage {
  coverType: string;
  url: string;
  remoteUrl?: string;
}

// A show or a movie normalised to one shape so the unified Library, Add New and
// Library Import pages can share a single card, grid and list.
export interface MediaItem {
  id: number;
  selectKey: string;
  type: MediaType;
  title: string;
  sortTitle: string;
  year: number;
  monitored: boolean;
  overview: string;
  status: string;
  certification: string;
  genres: string[];
  images: MediaImage[];
  sizeOnDisk: number;
  added: string;
  rating?: number;
  hasFile: boolean;
  episodeCount: number;
  episodeFileCount: number;
  seriesType?: string;
  link: string;
  series?: Series;
  movie?: Movie;
}

export interface SelectedMediaIds {
  seriesIds: number[];
  movieIds: number[];
}

export function splitSelectKeys(
  selectKeys: Array<string | number>
): SelectedMediaIds {
  return selectKeys.reduce<SelectedMediaIds>(
    (acc, selectKeyValue) => {
      const selectKey = String(selectKeyValue);
      const separatorIndex = selectKey.indexOf('-');
      const type = selectKey.slice(0, separatorIndex);
      const id = Number(selectKey.slice(separatorIndex + 1));

      if (type === 'series') {
        acc.seriesIds.push(id);
      } else if (type === 'movie') {
        acc.movieIds.push(id);
      }

      return acc;
    },
    { seriesIds: [], movieIds: [] }
  );
}

function serviceOf(type: MediaType): ServiceId {
  return type === 'movie' ? 'movies' : 'series';
}

export function getMediaItemPosterUrl(item: MediaItem): string | undefined {
  const image =
    item.images.find((image) => image.coverType === 'poster') ?? item.images[0];

  if (!image) {
    return undefined;
  }

  const source =
    item.type === 'movie' ? image.remoteUrl || image.url : image.url;

  return getMediaCoverUrl(serviceOf(item.type), source);
}

export function getMediaItemProgress(item: MediaItem): number {
  if (item.type === 'movie') {
    return item.hasFile ? 100 : 0;
  }

  return item.episodeCount > 0
    ? (item.episodeFileCount / item.episodeCount) * 100
    : 0;
}

export function seriesToMediaItem(series: Series): MediaItem {
  const statistics = series.statistics;

  return {
    id: series.id,
    selectKey: `series-${series.id}`,
    type: 'series',
    title: series.title,
    sortTitle: series.sortTitle,
    year: series.year,
    monitored: series.monitored,
    overview: series.overview,
    status: series.status,
    certification: series.certification,
    genres: series.genres ?? [],
    images: series.images ?? [],
    sizeOnDisk: statistics?.sizeOnDisk ?? 0,
    added: series.added,
    rating: series.ratings?.value,
    hasFile: (statistics?.episodeFileCount ?? 0) > 0,
    episodeCount: statistics?.episodeCount ?? 0,
    episodeFileCount: statistics?.episodeFileCount ?? 0,
    seriesType: series.seriesType,
    link: `/series/${series.titleSlug}`,
    series,
  };
}

export function movieToMediaItem(movie: Movie): MediaItem {
  const ratings = movie.ratings
    ? Object.values(movie.ratings).map((rating) => rating.value)
    : [];

  return {
    id: movie.id,
    selectKey: `movie-${movie.id}`,
    type: 'movie',
    title: movie.title,
    sortTitle: movie.sortTitle,
    year: movie.year,
    monitored: movie.monitored,
    overview: movie.overview,
    status: movie.status,
    certification: movie.certification ?? '',
    genres: movie.genres ?? [],
    images: movie.images ?? [],
    sizeOnDisk: movie.sizeOnDisk ?? 0,
    added: movie.added,
    rating: ratings.length ? Math.max(...ratings) : undefined,
    hasFile: movie.hasFile,
    episodeCount: 0,
    episodeFileCount: 0,
    link: `/movie/${movie.id}`,
    movie,
  };
}
