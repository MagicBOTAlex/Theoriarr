import Language from 'Language/Language';
import { MovieFile } from 'MovieFile/MovieFile';
import Quality from 'Quality/Quality';
import getMediaCoverUrl from 'Utilities/MediaCover';

export type MovieMonitor = 'movieOnly' | 'movieAndCollection' | 'none';

export type MovieStatus =
  | 'tba'
  | 'announced'
  | 'inCinemas'
  | 'released'
  | 'deleted';

export type MovieAvailability = 'announced' | 'inCinemas' | 'released';

export type CoverType =
  | 'poster'
  | 'fanart'
  | 'headshot'
  | 'banner'
  | 'logo'
  | 'clearlogo'
  | 'disc'
  | 'thumb';

export interface MovieImage {
  coverType: CoverType;
  url: string;
  remoteUrl?: string;
}

export interface Collection {
  tmdbId: number;
  title: string;
}

export interface AlternativeTitle {
  sourceType: string;
  title: string;
}

export interface Statistics {
  movieFileCount: number;
  sizeOnDisk: number;
  releaseGroups?: string[];
  movieFileQualities?: Quality[];
}

export interface RatingValues {
  value: number;
  votes?: number;
  type?: string;
}

export interface Ratings {
  imdb?: RatingValues;
  tmdb?: RatingValues;
  metacritic?: RatingValues;
  rottenTomatoes?: RatingValues;
  trakt?: RatingValues;
}

export interface Movie {
  id: number;
  title: string;
  titleSlug?: string;
  originalTitle?: string;
  originalLanguage?: Language;
  sortTitle: string;
  year: number;
  overview: string;
  status: string;
  monitored: boolean;
  hasFile: boolean;
  isAvailable?: boolean;
  movieFileId: number;
  movieFile?: MovieFile;
  sizeOnDisk: number;
  runtime: number;
  studio: string;
  genres: string[];
  certification: string;
  tmdbId: number;
  imdbId: string;
  youTubeTrailerId?: string;
  added: string;
  minimumAvailability: string;
  images: MovieImage[];
  alternateTitles?: AlternativeTitle[];
  collection?: Collection;
  keywords?: string[];
  popularity?: number;
  website?: string;
  path?: string;
  rootFolderPath?: string;
  qualityProfileId?: number;
  tags?: number[];
  ratings?: Ratings;
  statistics?: Statistics;
  inCinemas?: string;
  physicalRelease?: string;
  digitalRelease?: string;
  releaseDate?: string;
}

export function getMoviePosterUrl(movie: Movie): string | undefined {
  const image =
    movie.images?.find((item) => item.coverType === 'poster') ??
    movie.images?.[0];

  if (!image) {
    return undefined;
  }

  return getMediaCoverUrl('movies', image.url || image.remoteUrl);
}

export function getMovieFanartUrl(movie: Movie): string | undefined {
  const image = movie.images?.find((item) => item.coverType === 'fanart');

  if (!image) {
    return undefined;
  }

  return getMediaCoverUrl('movies', image.url || image.remoteUrl);
}

export function getMovieImages(movie: Movie, coverType: string) {
  return movie.images?.filter((item) => item.coverType === coverType) ?? [];
}
