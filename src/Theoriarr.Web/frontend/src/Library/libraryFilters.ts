import moment from 'moment-timezone';
import { FilterBuilderTag } from 'Components/Filter/Builder/FilterBuilderRowValue';
import { Filter, FilterBuilderProp } from 'Filters/Filter';
import {
  filterBuilderTypes,
  filterBuilderValueTypes,
  sortDirections,
} from 'Helpers/Props';
import { FilterType } from 'Helpers/Props/filterTypes';
import getFilterTypePredicate from 'Helpers/Props/getFilterTypePredicate';
import { SortDirection } from 'Helpers/Props/sortDirections';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import { MediaItem } from './MediaItem';

const dateFilterPredicate = (
  itemDate: string | undefined,
  filterValue: string | Date,
  type: FilterType
): boolean => {
  if (!itemDate) {
    return false;
  }

  const predicate = getFilterTypePredicate(type);
  return predicate(itemDate, filterValue);
};

export const LIBRARY_FILTERS: Filter[] = [
  {
    key: 'all',
    label: () => translate('All'),
    filters: [],
  },
  {
    key: 'seriesOnly',
    label: () => translate('Series'),
    filters: [
      {
        key: 'type',
        value: 'series',
        type: 'equal',
      },
    ],
  },
  {
    key: 'movieOnly',
    label: () => translate('Movies'),
    filters: [
      {
        key: 'type',
        value: 'movie',
        type: 'equal',
      },
    ],
  },
  {
    key: 'monitored',
    label: () => translate('MonitoredOnly'),
    filters: [
      {
        key: 'monitored',
        value: [true],
        type: 'equal',
      },
    ],
  },
  {
    key: 'unmonitored',
    label: () => translate('UnmonitoredOnly'),
    filters: [
      {
        key: 'monitored',
        value: [false],
        type: 'equal',
      },
    ],
  },
  {
    key: 'continuing',
    label: () => translate('ContinuingOnly'),
    filters: [
      {
        key: 'type',
        value: 'series',
        type: 'equal',
      },
      {
        key: 'status',
        value: 'continuing',
        type: 'equal',
      },
    ],
  },
  {
    key: 'ended',
    label: () => translate('EndedOnly'),
    filters: [
      {
        key: 'type',
        value: 'series',
        type: 'equal',
      },
      {
        key: 'status',
        value: 'ended',
        type: 'equal',
      },
    ],
  },
  {
    key: 'missing',
    label: () => translate('MissingEpisodes'),
    filters: [
      {
        key: 'type',
        value: 'series',
        type: 'equal',
      },
      {
        key: 'missing',
        value: [true],
        type: 'equal',
      },
    ],
  },
  {
    key: 'movieMissing',
    label: () => translate('MissingMovies'),
    filters: [
      {
        key: 'type',
        value: 'movie',
        type: 'equal',
      },
      {
        key: 'monitored',
        value: [true],
        type: 'equal',
      },
      {
        key: 'hasFile',
        value: [false],
        type: 'equal',
      },
    ],
  },
  {
    key: 'missingAny',
    label: () => translate('Missing'),
    filters: [
      {
        key: 'missingAny',
        value: [true],
        type: 'equal',
      },
    ],
  },
  {
    key: 'wanted',
    label: () => translate('Wanted'),
    filters: [
      {
        key: 'type',
        value: 'movie',
        type: 'equal',
      },
      {
        key: 'monitored',
        value: [true],
        type: 'equal',
      },
      {
        key: 'hasFile',
        value: [false],
        type: 'equal',
      },
      {
        key: 'isAvailable',
        value: [true],
        type: 'equal',
      },
    ],
  },
  {
    key: 'cutoffUnmet',
    label: () => translate('CutoffUnmet'),
    filters: [
      {
        key: 'type',
        value: 'movie',
        type: 'equal',
      },
      {
        key: 'monitored',
        value: [true],
        type: 'equal',
      },
      {
        key: 'hasFile',
        value: [true],
        type: 'equal',
      },
      {
        key: 'qualityCutoffNotMet',
        value: [true],
        type: 'equal',
      },
    ],
  },
];

export const LIBRARY_SORT_PREDICATES = {
  status: (item: MediaItem, _: SortDirection) => {
    if (item.type === 'movie') {
      return movieReleaseStatusSort(item);
    }

    let result = 0;

    if (item.monitored) {
      result += 2;
    }

    if (item.status === 'continuing') {
      result++;
    }

    return result;
  },

  movieStatus: (item: MediaItem, _: SortDirection) => {
    const movie = item.movie;

    if (!movie) {
      return '';
    }

    let result = 0;
    let qualityName = '';

    if (movie.isAvailable) {
      result++;
    }

    if (movie.monitored) {
      result += 2;
    }

    if (movie.movieFile) {
      if (movie.movieFile.qualityCutoffNotMet) {
        result += 4;
      } else {
        result += 8;
      }

      qualityName = movie.movieFile.quality?.quality?.name ?? '';
    }

    return result.toString().padStart(2, '0') + qualityName;
  },

  movieReleaseStatus: (item: MediaItem, _: SortDirection) => {
    return movieReleaseStatusSort(item);
  },

  sizeOnDisk: (item: MediaItem, _: SortDirection) => {
    return item.sizeOnDisk ?? 0;
  },

  averageSizePerEpisode: (item: MediaItem, _: SortDirection) => {
    const totalEpisodeCount = item.series?.statistics?.totalEpisodeCount ?? 0;

    return totalEpisodeCount > 0 ? item.sizeOnDisk / totalEpisodeCount : 0;
  },

  network: (item: MediaItem, _: SortDirection) => {
    return item.series?.network?.toLowerCase() ?? '';
  },

  studio: (item: MediaItem, _: SortDirection) => {
    return item.movie?.studio?.toLowerCase() ?? '';
  },

  originalTitle: (item: MediaItem, _: SortDirection) => {
    return item.movie?.originalTitle?.toLowerCase() ?? '';
  },

  collection: (item: MediaItem, _: SortDirection) => {
    return item.movie?.collection?.title ?? '';
  },

  nextAiring: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.series?.nextAiring, direction, false);
  },

  previousAiring: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.series?.previousAiring, direction, true);
  },

  inCinemas: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.movie?.inCinemas, direction, true);
  },

  physicalRelease: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.movie?.physicalRelease, direction, true);
  },

  digitalRelease: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.movie?.digitalRelease, direction, true);
  },

  releaseDate: (item: MediaItem, direction: SortDirection) => {
    return dateSort(item.movie?.releaseDate, direction, true);
  },

  episodeProgress: (item: MediaItem, _: SortDirection) => {
    const statistics = item.series?.statistics;

    const episodeCount = statistics?.episodeCount ?? 0;
    const episodeFileCount = statistics?.episodeFileCount ?? 0;

    const progress = episodeCount
      ? (episodeFileCount / episodeCount) * 100
      : 100;

    return progress + episodeCount / 1000000;
  },

  episodeCount: (item: MediaItem, _: SortDirection) => {
    return item.series?.statistics?.totalEpisodeCount ?? 0;
  },

  seasonCount: (item: MediaItem, _: SortDirection) => {
    return item.series?.statistics?.seasonCount ?? 0;
  },

  latestSeason: (item: MediaItem, _: SortDirection) => {
    return item.series?.statistics?.seasonCount ?? 0;
  },

  originalLanguage: (item: MediaItem, _: SortDirection) => {
    return (
      item.series?.originalLanguage?.name ??
      item.movie?.originalLanguage?.name ??
      ''
    );
  },

  originalCountry: (item: MediaItem, _: SortDirection) => {
    return item.series?.originalCountry ?? '';
  },

  ratings: (item: MediaItem, _: SortDirection) => {
    return item.rating ?? 0;
  },

  tmdbRating: (item: MediaItem, _: SortDirection) => {
    return item.movie?.ratings?.tmdb?.value ?? 0;
  },

  imdbRating: (item: MediaItem, _: SortDirection) => {
    return item.movie?.ratings?.imdb?.value ?? 0;
  },

  rottenTomatoesRating: (item: MediaItem, _: SortDirection) => {
    return item.movie?.ratings?.rottenTomatoes?.value ?? -1;
  },

  traktRating: (item: MediaItem, _: SortDirection) => {
    return item.movie?.ratings?.trakt?.value ?? 0;
  },

  popularity: (item: MediaItem, _: SortDirection) => {
    return item.movie?.popularity ?? 0;
  },

  runtime: (item: MediaItem, _: SortDirection) => {
    return item.movie?.runtime ?? 0;
  },

  monitorNewItems: (item: MediaItem, _: SortDirection) => {
    return item.series?.monitorNewItems === 'all' ? 1 : 0;
  },

  useSceneNumbering: (item: MediaItem, _: SortDirection) => {
    return item.series?.useSceneNumbering ? 1 : 0;
  },

  releaseGroups: (item: MediaItem, _: SortDirection) => {
    const groups =
      item.movie?.statistics?.releaseGroups ??
      item.series?.statistics?.releaseGroups ??
      [];

    return groups.length
      ? groups.map((group) => group.toLowerCase()).sort()
      : undefined;
  },

  movieFileQualities: (item: MediaItem, _: SortDirection) => {
    return (item.movie?.statistics?.movieFileQualities ?? [])
      .map((q) => q.name)
      .join(', ');
  },

  qualityProfileId: (item: MediaItem, _: SortDirection) => {
    return item.series?.qualityProfileId ?? item.movie?.qualityProfileId ?? 0;
  },

  rootFolderPath: (item: MediaItem, _: SortDirection) => {
    return item.series?.rootFolderPath ?? item.movie?.rootFolderPath ?? '';
  },

  keywords: (item: MediaItem, _: SortDirection) => {
    return (item.movie?.keywords ?? []).join(', ');
  },
} as const;

function movieReleaseStatusSort(item: MediaItem) {
  const { status, monitored } = item;

  let result = 0;

  if (monitored) {
    result += 4;
  }

  if (status === 'announced') {
    result++;
  }

  if (status === 'inCinemas') {
    result += 2;
  }

  if (status === 'released') {
    result += 3;
  }

  return result;
}

function dateSort(
  value: string | undefined,
  direction: SortDirection,
  invertMissing: boolean
) {
  if (value) {
    return moment(value).unix();
  }

  if (direction === sortDirections.DESCENDING) {
    return invertMissing ? Number.MAX_VALUE * -1 : 0;
  }

  return Number.MAX_VALUE;
}

function isItemMissing(item: MediaItem) {
  if (item.type === 'movie') {
    return item.monitored && !item.hasFile;
  }

  const statistics = item.series?.statistics;
  const episodeCount = statistics?.episodeCount ?? 0;
  const episodeFileCount = statistics?.episodeFileCount ?? 0;

  return episodeCount - episodeFileCount > 0;
}

export const LIBRARY_FILTER_PREDICATES = {
  type: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.type, filterValue);
  },

  isAvailable: (item: MediaItem, filterValue: boolean, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.isAvailable ?? false, filterValue);
  },

  missing: (item: MediaItem, _filterValue: boolean) => {
    const statistics = item.series?.statistics;
    const episodeCount = statistics?.episodeCount ?? 0;
    const episodeFileCount = statistics?.episodeFileCount ?? 0;
    return episodeCount - episodeFileCount > 0;
  },

  missingAny: (item: MediaItem, filterValue: boolean, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(isItemMissing(item), filterValue);
  },

  qualityCutoffNotMet: (item: MediaItem, _filterValue: boolean) => {
    return item.movie?.movieFile?.qualityCutoffNotMet ?? false;
  },

  status: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.status, filterValue);
  },

  movieReleaseStatus: (
    item: MediaItem,
    filterValue: string,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.status ?? '', filterValue);
  },

  movieStatus: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.status ?? '', filterValue);
  },

  minimumAvailability: (
    item: MediaItem,
    filterValue: string,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.minimumAvailability ?? '', filterValue);
  },

  episodeProgress: (item: MediaItem, filterValue: number, type: FilterType) => {
    const statistics = item.series?.statistics;
    const episodeCount = statistics?.episodeCount ?? 0;
    const episodeFileCount = statistics?.episodeFileCount ?? 0;

    const progress = episodeCount
      ? (episodeFileCount / episodeCount) * 100
      : 100;

    const predicate = getFilterTypePredicate(type);
    return predicate(progress, filterValue);
  },

  nextAiring: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.series?.nextAiring, filterValue, type);
  },

  previousAiring: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.series?.previousAiring, filterValue, type);
  },

  inCinemas: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.movie?.inCinemas, filterValue, type);
  },

  physicalRelease: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.movie?.physicalRelease, filterValue, type);
  },

  digitalRelease: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.movie?.digitalRelease, filterValue, type);
  },

  releaseDate: (
    item: MediaItem,
    filterValue: string | Date,
    type: FilterType
  ) => {
    return dateFilterPredicate(item.movie?.releaseDate, filterValue, type);
  },

  added: (item: MediaItem, filterValue: string | Date, type: FilterType) => {
    return dateFilterPredicate(item.added, filterValue, type);
  },

  network: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.series?.network ?? '', filterValue);
  },

  studio: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.studio ?? '', filterValue);
  },

  collection: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.collection?.title ?? '', filterValue);
  },

  originalTitle: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.originalTitle ?? '', filterValue);
  },

  originalLanguage: (
    item: MediaItem,
    filterValue: string,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    const languageName =
      item.series?.originalLanguage?.name ??
      item.movie?.originalLanguage?.name ??
      '';
    return predicate(languageName, filterValue);
  },

  originalCountry: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.series?.originalCountry ?? '', filterValue);
  },

  ratings: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    const value = item.rating ?? 0;
    return predicate(value * 10, filterValue);
  },

  ratingVotes: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    const votes = item.series?.ratings?.votes ?? 0;
    return predicate(votes, filterValue);
  },

  tmdbRating: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate((item.movie?.ratings?.tmdb?.value ?? 0) * 10, filterValue);
  },

  tmdbVotes: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.ratings?.tmdb?.votes ?? 0, filterValue);
  },

  imdbRating: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.ratings?.imdb?.value ?? 0, filterValue);
  },

  imdbVotes: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.ratings?.imdb?.votes ?? 0, filterValue);
  },

  rottenTomatoesRating: (
    item: MediaItem,
    filterValue: number,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(
      item.movie?.ratings?.rottenTomatoes?.value ?? 0,
      filterValue
    );
  },

  traktRating: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(
      (item.movie?.ratings?.trakt?.value ?? 0) * 10,
      filterValue
    );
  },

  traktVotes: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.ratings?.trakt?.votes ?? 0, filterValue);
  },

  popularity: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.popularity ?? 0, filterValue);
  },

  runtime: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.movie?.runtime ?? 0, filterValue);
  },

  seasonCount: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.series?.statistics?.seasonCount ?? 0, filterValue);
  },

  sizeOnDisk: (item: MediaItem, filterValue: number, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.sizeOnDisk ?? 0, filterValue);
  },

  averageSizePerEpisode: (
    item: MediaItem,
    filterValue: number,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    const totalEpisodeCount = item.series?.statistics?.totalEpisodeCount ?? 0;
    const averageSize =
      totalEpisodeCount > 0 ? item.sizeOnDisk / totalEpisodeCount : 0;
    return predicate(averageSize, filterValue);
  },

  releaseGroups: (item: MediaItem, filterValue: string[], type: FilterType) => {
    const releaseGroups =
      item.movie?.statistics?.releaseGroups ??
      item.series?.statistics?.releaseGroups ??
      [];
    const predicate = getFilterTypePredicate(type);
    return predicate(releaseGroups, filterValue);
  },

  releaseTypes: (item: MediaItem, filterValue: string[], type: FilterType) => {
    const releaseTypes = item.series?.statistics?.releaseTypes ?? [];
    const predicate = getFilterTypePredicate(type);
    return predicate(releaseTypes, filterValue);
  },

  episodeFileQualities: (
    item: MediaItem,
    filterValue: number[],
    type: FilterType
  ) => {
    const episodeFileQualities = (
      item.series?.statistics?.episodeFileQualities ?? []
    ).map((q) => q.id);
    const predicate = getFilterTypePredicate(type);
    return predicate(episodeFileQualities, filterValue);
  },

  movieFileQualities: (
    item: MediaItem,
    filterValue: number[],
    type: FilterType
  ) => {
    const movieFileQualities = (
      item.movie?.statistics?.movieFileQualities ?? []
    ).map((q) => q.id);
    const predicate = getFilterTypePredicate(type);
    return predicate(movieFileQualities, filterValue);
  },

  useSceneNumbering: (
    item: MediaItem,
    filterValue: boolean,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.series?.useSceneNumbering ?? false, filterValue);
  },

  monitorNewItems: (item: MediaItem, filterValue: string, type: FilterType) => {
    const predicate = getFilterTypePredicate(type);
    return predicate(item.series?.monitorNewItems ?? '', filterValue);
  },

  hasMissingSeason: (
    item: MediaItem,
    filterValue: boolean,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    const seasons = item.series?.seasons ?? [];

    const hasMissingSeason = seasons.some((season) => {
      const { seasonNumber } = season;
      const statistics = season.statistics;
      const episodeFileCount = statistics?.episodeFileCount ?? 0;
      const episodeCount = statistics?.episodeCount ?? 0;
      const totalEpisodeCount = statistics?.totalEpisodeCount ?? 0;

      return (
        seasonNumber > 0 &&
        totalEpisodeCount > 0 &&
        episodeCount === totalEpisodeCount &&
        episodeFileCount === 0
      );
    });

    return predicate(hasMissingSeason, filterValue);
  },

  seasonsMonitoredStatus: (
    item: MediaItem,
    filterValue: string,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    const seasons = item.series?.seasons ?? [];

    const { monitoredCount, unmonitoredCount } = seasons.reduce(
      (acc, { seasonNumber, monitored }) => {
        if (seasonNumber <= 0) {
          return acc;
        }

        if (monitored) {
          acc.monitoredCount++;
        } else {
          acc.unmonitoredCount++;
        }

        return acc;
      },
      { monitoredCount: 0, unmonitoredCount: 0 }
    );

    let seasonsMonitoredStatus = 'partial';

    if (monitoredCount === 0) {
      seasonsMonitoredStatus = 'none';
    } else if (unmonitoredCount === 0) {
      seasonsMonitoredStatus = 'all';
    }

    return predicate(seasonsMonitoredStatus, filterValue);
  },

  episodesMonitoredStatus: (
    item: MediaItem,
    filterValue: string,
    type: FilterType
  ) => {
    const predicate = getFilterTypePredicate(type);
    const seasons = item.series?.seasons ?? [];
    const statistics = item.series?.statistics;
    const monitoredEpisodeCount = statistics?.monitoredEpisodeCount ?? 0;
    const totalEpisodeCount = statistics?.totalEpisodeCount ?? 0;
    const specials = seasons.find((s) => s.seasonNumber === 0);

    const monitoredCount =
      monitoredEpisodeCount -
      (specials?.statistics?.monitoredEpisodeCount ?? 0);

    const totalCount =
      totalEpisodeCount - (specials?.statistics?.totalEpisodeCount ?? 0);

    let episodesMonitoredStatus = 'partial';

    if (monitoredCount === 0) {
      episodesMonitoredStatus = 'none';
    } else if (totalCount - monitoredCount === 0) {
      episodesMonitoredStatus = 'all';
    }

    return predicate(episodesMonitoredStatus, filterValue);
  },
} as const;

function seriesOptions(items: ReadonlyArray<MediaItem>) {
  return items.filter((item) => item.type === 'series');
}

function movieOptions(items: ReadonlyArray<MediaItem>) {
  return items.filter((item) => item.type === 'movie');
}

export const LIBRARY_FILTER_BUILDER: FilterBuilderProp<MediaItem>[] = [
  {
    name: 'monitored',
    label: () => translate('Monitored'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
  {
    name: 'status',
    label: () => translate('ReleaseStatus'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.SERIES_STATUS,
  },
  {
    name: 'movieReleaseStatus',
    label: () => translate('MovieReleaseStatus'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'isAvailable',
    label: () => translate('ConsideredAvailable'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
  {
    name: 'minimumAvailability',
    label: () => translate('MinimumAvailability'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'seriesType',
    label: () => translate('Type'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.SERIES_TYPES,
  },
  {
    name: 'title',
    label: () => translate('Title'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'originalTitle',
    label: () => translate('OriginalTitle'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'network',
    label: () => translate('Network'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const tagList = seriesOptions(items).reduce<
        FilterBuilderTag<string, string>[]
      >((acc, item) => {
        const network = item.series?.network;

        if (network) {
          acc.push({ id: network, name: network });
        }

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    },
  },
  {
    name: 'studio',
    label: () => translate('Studio'),
    type: filterBuilderTypes.EXACT,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const tagList = movieOptions(items).reduce<
        FilterBuilderTag<string, string>[]
      >((acc, item) => {
        const studio = item.movie?.studio;

        if (studio) {
          acc.push({ id: studio, name: studio });
        }

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    },
  },
  {
    name: 'collection',
    label: () => translate('Collection'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const tagList = movieOptions(items).reduce<
        FilterBuilderTag<string, string>[]
      >((acc, item) => {
        const collection = item.movie?.collection;

        if (collection?.title) {
          acc.push({ id: collection.title, name: collection.title });
        }

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    },
  },
  {
    name: 'qualityProfileId',
    label: () => translate('QualityProfile'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUALITY_PROFILE,
  },
  {
    name: 'nextAiring',
    label: () => translate('NextAiring'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'previousAiring',
    label: () => translate('PreviousAiring'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'added',
    label: () => translate('Added'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'seasonCount',
    label: () => translate('SeasonCount'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'episodeProgress',
    label: () => translate('EpisodeProgress'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'year',
    label: () => translate('Year'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'inCinemas',
    label: () => translate('InCinemas'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'physicalRelease',
    label: () => translate('PhysicalRelease'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'digitalRelease',
    label: () => translate('DigitalRelease'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'releaseDate',
    label: () => translate('ReleaseDate'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'runtime',
    label: () => translate('Runtime'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'path',
    label: () => translate('Path'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'rootFolderPath',
    label: () => translate('RootFolderPath'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'sizeOnDisk',
    label: () => translate('SizeOnDisk'),
    type: filterBuilderTypes.NUMBER,
    valueType: filterBuilderValueTypes.BYTES,
  },
  {
    name: 'averageSizePerEpisode',
    label: () => translate('AverageSizePerEpisode'),
    type: filterBuilderTypes.NUMBER,
    valueType: filterBuilderValueTypes.BYTES,
  },
  {
    name: 'genres',
    label: () => translate('Genres'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const tagList = items.reduce<FilterBuilderTag<string, string>[]>(
        (acc, item) => {
          (item.genres ?? []).forEach((genre) => {
            acc.push({ id: genre, name: genre });
          });

          return acc;
        },
        []
      );

      return tagList.sort(sortByProp('name'));
    },
  },
  {
    name: 'keywords',
    label: () => translate('Keywords'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const tagList = movieOptions(items).reduce<
        FilterBuilderTag<string, string>[]
      >((acc, item) => {
        (item.movie?.keywords ?? []).forEach((keyword) => {
          if (!acc.some((a) => a.id === keyword)) {
            acc.push({ id: keyword, name: keyword });
          }
        });

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    },
  },
  {
    name: 'originalLanguage',
    label: () => translate('OriginalLanguage'),
    type: filterBuilderTypes.EXACT,
    optionsSelector: function (items: ReadonlyArray<MediaItem>) {
      const languageList = items.reduce<FilterBuilderTag<string, string>[]>(
        (acc, item) => {
          const language =
            item.series?.originalLanguage ?? item.movie?.originalLanguage;

          if (language?.name) {
            acc.push({ id: language.name, name: language.name });
          }

          return acc;
        },
        []
      );

      return languageList.sort(sortByProp('name'));
    },
  },
  {
    name: 'releaseGroups',
    label: () => translate('ReleaseGroups'),
    type: filterBuilderTypes.ARRAY,
  },
  {
    name: 'releaseTypes',
    label: () => translate('ReleaseTypes'),
    type: filterBuilderTypes.ARRAY,
    valueType: filterBuilderValueTypes.RELEASE_TYPES,
  },
  {
    name: 'movieFileQualities',
    label: () => translate('MovieFileQualities'),
    type: filterBuilderTypes.ARRAY,
    valueType: filterBuilderValueTypes.QUALITY,
  },
  {
    name: 'episodeFileQualities',
    label: () => translate('EpisodeFileQualities'),
    type: filterBuilderTypes.ARRAY,
    valueType: filterBuilderValueTypes.QUALITY,
  },
  {
    name: 'ratings',
    label: () => translate('Rating'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'ratingVotes',
    label: () => translate('RatingVotes'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'tmdbRating',
    label: () => translate('TmdbRating'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'tmdbVotes',
    label: () => translate('TmdbVotes'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'imdbRating',
    label: () => translate('ImdbRating'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'imdbVotes',
    label: () => translate('ImdbVotes'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'rottenTomatoesRating',
    label: () => translate('RottenTomatoesRating'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'traktRating',
    label: () => translate('TraktRating'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'traktVotes',
    label: () => translate('TraktVotes'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'popularity',
    label: () => translate('Popularity'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'certification',
    label: () => translate('Certification'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'tags',
    label: () => translate('Tags'),
    type: filterBuilderTypes.ARRAY,
    valueType: filterBuilderValueTypes.TAG,
  },
  {
    name: 'useSceneNumbering',
    label: () => translate('SceneNumbering'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'monitorNewItems',
    label: () => translate('MonitorNewSeasons'),
    type: filterBuilderTypes.EXACT,
  },
  {
    name: 'hasMissingSeason',
    label: () => translate('HasMissingSeason'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
  {
    name: 'seasonsMonitoredStatus',
    label: () => translate('SeasonsMonitoredStatus'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.MONITORED_STATUS,
  },
  {
    name: 'episodesMonitoredStatus',
    label: () => translate('EpisodesMonitoredStatus'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.MONITORED_STATUS,
  },
];
