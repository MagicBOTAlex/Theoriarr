import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { create } from 'zustand';
import ModelBase from 'App/ModelBase';
import { FilterBuilderTag } from 'Components/Filter/Builder/FilterBuilderRowValue';
import type DownloadProtocol from 'DownloadClient/DownloadProtocol';
import { Filter, FilterBuilderProp } from 'Filters/Filter';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { applySort } from 'Helpers/Hooks/useOptionsStore';
import { filterBuilderTypes, filterBuilderValueTypes } from 'Helpers/Props';
import { FilterType } from 'Helpers/Props/filterTypes';
import getFilterTypePredicate from 'Helpers/Props/getFilterTypePredicate';
import { SortDirection } from 'Helpers/Props/sortDirections';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { AlternateTitle } from 'Series/Series';
import {
  getService,
  getServiceClientHeader,
  getServiceQueryKey,
  ServiceId,
} from 'Services';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import Rejection from 'typings/Rejection';
import sortByProp from 'Utilities/Array/sortByProp';
import getQueryPath from 'Utilities/Fetch/getQueryPath';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import enumToTitle from 'Utilities/String/enumToTitle';
import translate from 'Utilities/String/translate';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import {
  getReleaseOption,
  setReleaseOption,
  useReleaseOptions,
} from './releaseOptionsStore';
import { getReleaseSearchProgressActions } from './releaseSearchProgressStore';

export interface ReleaseEpisode {
  id: number;
  episodeFileId: number;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  title: string;
}

export interface Release extends ModelBase {
  parsedInfo: ParsedInfo;
  release: ReleaseInfo;
  decision: Decision;
  history?: ReleaseHistory;
  qualityWeight: number;
  languages: Language[];
  mappedMovieId?: number;
  mappedSeriesId?: number;
  mappedSeasonNumber?: number;
  mappedEpisodeNumbers?: number[];
  mappedAbsoluteEpisodeNumbers?: number[];
  mappedEpisodeInfo: ReleaseEpisode[];
  episodeRequested: boolean;
  downloadAllowed: boolean;
  releaseWeight: number;
  customFormats: CustomFormat[];
  customFormatScore: number;
  sceneMapping?: AlternateTitle;
  movieRequested?: boolean;
  movieIndexerFlags?: string[];
}

// The movie domain is Radarr's flat V3 `ReleaseResource` (no nested parsedInfo/
// release/decision), so it is normalised into the shared `Release` shape.
export interface MovieRelease {
  id: number;
  guid: string;
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  history?: ReleaseHistory;
  qualityWeight: number;
  age: number;
  ageHours: number;
  ageMinutes: number;
  size: number;
  indexerId: number;
  indexer: string;
  releaseGroup?: string;
  releaseHash?: string;
  title: string;
  movieTitles?: string[];
  languages: Language[];
  mappedMovieId?: number;
  approved: boolean;
  temporarilyRejected: boolean;
  rejected: boolean;
  tmdbId: number;
  imdbId?: string;
  rejections: string[];
  publishDate: string;
  commentUrl?: string;
  downloadUrl: string;
  infoUrl?: string;
  movieRequested: boolean;
  downloadAllowed: boolean;
  releaseWeight: number;
  edition?: string;
  magnetUrl?: string;
  infoHash?: string;
  seeders?: number;
  leechers?: number;
  protocol: DownloadProtocol;
  indexerFlags?: string[];
  movieId?: number;
  downloadClientId?: number;
  shouldOverride?: boolean;
}

const mapMovieRelease = (movie: MovieRelease): Release => {
  return {
    id: movie.id,
    parsedInfo: {
      quality: movie.quality,
      releaseGroup: movie.releaseGroup ?? '',
      releaseHash: movie.releaseHash ?? '',
      fullSeason: false,
      seasonNumber: null,
      seriesTitle: movie.movieTitles?.[0] ?? movie.title,
      episodeNumbers: [],
      isDaily: false,
      isAbsoluteNumbering: false,
      isPossibleSpecialEpisode: false,
      special: false,
    },
    release: {
      guid: movie.guid,
      age: movie.age,
      ageHours: movie.ageHours,
      ageMinutes: movie.ageMinutes,
      size: movie.size,
      indexerId: movie.indexerId,
      indexer: movie.indexer,
      title: movie.title,
      tvdbId: movie.tmdbId,
      tvRageId: 0,
      publishDate: movie.publishDate,
      commentUrl: movie.commentUrl ?? '',
      downloadUrl: movie.downloadUrl,
      infoUrl: movie.infoUrl ?? '',
      protocol: movie.protocol,
      indexerFlags: 0,
      seeders: movie.seeders,
      leechers: movie.leechers,
      magnetUrl: movie.magnetUrl,
      infoHash: movie.infoHash,
    },
    decision: {
      approved: movie.approved,
      temporarilyRejected: movie.temporarilyRejected,
      rejected: movie.rejected,
      rejections: (movie.rejections ?? []).map((message) => ({
        reason: message,
        message,
        type: movie.temporarilyRejected ? 'temporary' : 'permanent',
      })),
    },
    history: movie.history,
    qualityWeight: movie.qualityWeight,
    languages: movie.languages ?? [],
    mappedMovieId: movie.mappedMovieId ?? movie.movieId,
    mappedEpisodeInfo: [],
    episodeRequested: false,
    downloadAllowed: movie.downloadAllowed,
    releaseWeight: movie.releaseWeight,
    customFormats: movie.customFormats ?? [],
    customFormatScore: movie.customFormatScore,
    movieRequested: movie.movieRequested,
    movieIndexerFlags: movie.indexerFlags,
  };
};

export interface ParsedInfo {
  quality: QualityModel;
  releaseGroup: string;
  releaseHash: string;
  fullSeason: boolean;
  seasonNumber: number | null;
  seriesTitle: string;
  episodeNumbers: number[];
  absoluteEpisodeNumbers?: number[];
  isDaily: boolean;
  isAbsoluteNumbering: boolean;
  isPossibleSpecialEpisode: boolean;
  special: boolean;
}

export interface ReleaseInfo {
  guid: string;
  age: number;
  ageHours: number;
  ageMinutes: number;
  size: number;
  indexerId: number;
  indexer: string;
  title: string;
  tvdbId: number;
  tvRageId: number;
  publishDate: string;
  commentUrl: string;
  downloadUrl: string;
  infoUrl: string;
  protocol: DownloadProtocol;
  indexerFlags: number;
  seeders?: number;
  leechers?: number;
  magnetUrl?: string;
  infoHash?: string;
}

export interface Decision {
  approved: boolean;
  temporarilyRejected: boolean;
  rejected: boolean;
  rejections: Rejection[];
}

export interface ReleaseHistory {
  grabbed: string;
  failed: string;
}

export interface ReleaseSearchCacheStatus {
  available: boolean;
  cachedAt?: string;
  count?: number;
}

// The server caches the most recent completed search per target so the user can choose between
// reusing it and searching the indexers again.
type ReleaseSearchCacheDecision = 'pending' | 'prompt' | 'search' | 'use';

export const FILTERS: Filter[] = [
  {
    key: 'all',
    label: () => translate('All'),
    filters: [],
  },
  {
    key: 'season-pack',
    label: () => translate('SeasonPack'),
    filters: [
      {
        key: 'fullSeason',
        value: [true],
        type: 'equal',
      },
    ],
  },
  {
    key: 'not-season-pack',
    label: () => translate('NotSeasonPack'),
    filters: [
      {
        key: 'fullSeason',
        value: [false],
        type: 'equal',
      },
    ],
  },
];

export const MOVIE_FILTERS: Filter[] = [
  {
    key: 'all',
    label: () => translate('All'),
    filters: [],
  },
];

export const FILTER_BUILDER: FilterBuilderProp<Release>[] = [
  {
    name: 'title',
    label: () => translate('Title'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'age',
    label: () => translate('Age'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'protocol',
    label: () => translate('Protocol'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.PROTOCOL,
  },
  {
    name: 'indexerId',
    label: () => translate('Indexer'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.INDEXER,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    type: filterBuilderTypes.NUMBER,
    valueType: filterBuilderValueTypes.BYTES,
  },
  {
    name: 'seeders',
    label: () => translate('Seeders'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'peers',
    label: () => translate('Peers'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUALITY,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function (items) {
      const languageList = items.reduce<FilterBuilderTag<string, string>[]>(
        (acc, release) => {
          release.languages.forEach((language) => {
            acc.push({
              id: language.name,
              name: language.name,
            });
          });

          return acc;
        },
        []
      );

      return languageList.sort(
        sortByProp<FilterBuilderTag<string, string>, 'name'>('name')
      );
    },
  },
  {
    name: 'customFormatScore',
    label: () => translate('CustomFormatScore'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'rejectionCount',
    label: () => translate('RejectionCount'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'rejections',
    label: () => translate('Rejections'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function () {
      return getReleaseOption('rejectionFilterTags');
    },
  },
  {
    name: 'fullSeason',
    label: () => translate('SeasonPack'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
  {
    name: 'episodeRequested',
    label: () => translate('EpisodeRequested'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
];

const FILTER_PREDICATES = {
  age: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(item.release.age, value, type);
  },

  episodeRequested: (item: Release, value: boolean, type: FilterType) => {
    return applyFilterPredicate(item.episodeRequested, value, type);
  },

  fullSeason: (item: Release, value: boolean, type: FilterType) => {
    return applyFilterPredicate(item.parsedInfo.fullSeason, value, type);
  },

  indexerId: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(item.release.indexerId, value, type);
  },

  languages: (item: Release, filterValue: string[], type: FilterType) => {
    const languages = item.languages.map((language) => language.name);

    return applyFilterPredicate(languages, filterValue, type);
  },

  peers: (item: Release, value: number, type: FilterType) => {
    const seeders = item.release.seeders || 0;
    const leechers = item.release.leechers || 0;
    const peers = seeders + leechers;

    return applyFilterPredicate(peers, value, type);
  },

  protocol: (item: Release, value: DownloadProtocol, type: FilterType) => {
    return applyFilterPredicate(item.release.protocol, value, type);
  },

  quality: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(
      item.parsedInfo.quality.quality.id,
      value,
      type
    );
  },

  rejectionCount: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(item.decision.rejections.length, value, type);
  },

  rejections: (item: Release, value: string[], type: FilterType) => {
    return applyFilterPredicate(
      item.decision.rejections.map((r) => r.reason),
      value,
      type
    );
  },

  seeders: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(item.release.seeders ?? 0, value, type);
  },

  size: (item: Release, value: number, type: FilterType) => {
    return applyFilterPredicate(item.release.size, value, type);
  },

  title: (item: Release, value: string, type: FilterType) => {
    return applyFilterPredicate(item.release.title, value, type);
  },
} as const;

const SORT_PREDICATES = {
  age: function (item: Release, _direction: SortDirection) {
    return item.release.ageMinutes;
  },

  indexer: (item: Release, _direction: SortDirection) => {
    return item.release.indexerId;
  },

  indexerFlags: (item: Release, _direction: SortDirection) => {
    return item.release.indexerFlags;
  },

  languages: (item: Release, _direction: SortDirection) => {
    if (item.languages.length > 1) {
      return 10000;
    }

    return item.languages[0]?.id ?? 0;
  },

  peers: (item: Release, _direction: SortDirection) => {
    const seeders = item.release.seeders || 0;
    const leechers = item.release.leechers || 0;

    return seeders * 1000000 + leechers;
  },

  protocol: (item: Release, _direction: SortDirection) => {
    return item.release.protocol;
  },

  qualityWeight: (item: Release, _direction: SortDirection) => {
    return item.qualityWeight;
  },

  rejections: (item: Release, _direction: SortDirection) => {
    const rejections = item.decision.rejections;
    const releaseWeight = item.releaseWeight;

    if (rejections.length !== 0) {
      return releaseWeight + 1000000;
    }

    return releaseWeight;
  },

  size: (item: Release, _direction: SortDirection) => {
    return item.release.size;
  },

  title: (item: Release, _direction: SortDirection) => {
    return item.release.title;
  },
} as const;

interface ReleaseStore {
  sortKey: string;
  sortDirection: SortDirection;
}

const releaseStore = create<ReleaseStore>(() => ({
  sortKey: 'releaseWeight',
  sortDirection: 'ascending',
}));

const THIRTY_MINUTES = 30 * 60 * 1000;

// The search runs in the backend, so tell it to stop when the client goes away. keepalive lets
// the request survive the page unload.
const cancelReleaseSearch = (service: ServiceId, searchId: string) => {
  const { apiKey } = getService(service);
  const path = `${getQueryPath(
    '/release',
    service
  )}?searchId=${encodeURIComponent(searchId)}`;

  return fetch(path, {
    method: 'DELETE',
    headers: {
      'X-Api-Key': apiKey,
      ...getServiceClientHeader(service),
    },
    keepalive: true,
  }).catch(() => {
    // The request may be dropped while the page unloads; there is nothing to recover.
  });
};

const useReleases = (payload: InteractiveSearchPayload) => {
  const customFilters = useCustomFiltersList('releases');
  const {
    episodeSelectedFilterKey,
    seasonSelectedFilterKey,
    movieSelectedFilterKey,
  } = useReleaseOptions();

  const { sortKey, sortDirection } = releaseStore();

  // Correlates this request with the per-indexer progress the backend broadcasts over
  // SignalR while the search is in flight.
  const searchId = useMemo(
    () => `release-${Math.random().toString(36).slice(2, 10)}`,
    []
  );

  const isMovie = 'movieId' in payload;
  const service: ServiceId = isMovie ? 'movies' : 'series';

  const seriesSelectedFilterKey =
    'seriesId' in payload ? seasonSelectedFilterKey : episodeSelectedFilterKey;
  const selectedFilterKey = isMovie
    ? movieSelectedFilterKey
    : seriesSelectedFilterKey;

  const [cacheDecision, setCacheDecision] =
    useState<ReleaseSearchCacheDecision>('pending');

  // Ask the server whether a recent search for this target is still cached before running a
  // new one. The check is cheap and does not start a search itself.
  const cacheStatus = useApiQuery<ReleaseSearchCacheStatus>({
    service,
    path: '/release/cached',
    queryParams: {
      ...payload,
    },
    queryOptions: {
      staleTime: 0,
      gcTime: 0,
      refetchOnWindowFocus: false,
      retry: false,
    },
  });

  useEffect(() => {
    if (cacheDecision !== 'pending' || !cacheStatus.isFetched) {
      return;
    }

    setCacheDecision(
      !cacheStatus.error && cacheStatus.data?.available ? 'prompt' : 'search'
    );
  }, [
    cacheDecision,
    cacheStatus.isFetched,
    cacheStatus.error,
    cacheStatus.data,
  ]);

  const useCached = cacheDecision === 'use';
  const isSearchEnabled = cacheDecision === 'search' || useCached;

  const { data, queryKey, ...result } = useApiQuery<Release[] | MovieRelease[]>(
    {
      service,
      path: '/release',
      queryParams: {
        ...payload,
        searchId,
        useCached: useCached ? true : undefined,
      },
      queryOptions: {
        enabled: isSearchEnabled,
        // Cache and stale times set to 30 minutes
        staleTime: THIRTY_MINUTES,
        gcTime: THIRTY_MINUTES,
        // Disable refetch on window focus to prevent refetching when the user switch tabs
        refetchOnWindowFocus: false,
        retry: false,
      },
    }
  );

  const releases = useMemo(
    () =>
      (data ?? []).map((release) =>
        isMovie
          ? mapMovieRelease(release as MovieRelease)
          : (release as Release)
      ),
    [data, isMovie]
  );

  const { data: filteredData, totalItems } = useMemo(
    () =>
      clientSideFilterAndSort<Release, typeof FILTER_PREDICATES>(releases, {
        selectedFilterKey,
        filters: FILTERS,
        filterPredicates: FILTER_PREDICATES,
        customFilters,
        sortKey,
        sortDirection,
        sortPredicates: SORT_PREDICATES,
      }),
    [releases, selectedFilterKey, customFilters, sortKey, sortDirection]
  );

  useEffect(() => {
    if (!releases.length) {
      return;
    }

    // Get existing rejection tags as a map for easy lookup
    const rejectionsMap = new Map(
      getReleaseOption('rejectionFilterTags').map((tag) => [tag.id, tag])
    );

    releases.forEach((release) => {
      release.decision.rejections.forEach((rejection) => {
        if (!rejectionsMap.has(rejection.reason)) {
          rejectionsMap.set(rejection.reason, {
            id: rejection.reason,
            name: enumToTitle(rejection.reason),
          });
        }
      });
    });

    const rejections = Array.from(rejectionsMap.values()).sort(
      sortByProp<FilterBuilderTag<string, string>, 'name'>('name')
    );

    setReleaseOption('rejectionFilterTags', rejections);
  }, [releases]);

  useEffect(() => {
    if (cacheDecision !== 'search') {
      return;
    }

    getReleaseSearchProgressActions().beginSearch(searchId);

    const handlePageHide = () => {
      cancelReleaseSearch(service, searchId);
    };

    // Stop the background search when the tab is closed or navigated away from.
    window.addEventListener('pagehide', handlePageHide);

    return () => {
      window.removeEventListener('pagehide', handlePageHide);
      cancelReleaseSearch(service, searchId);
      getReleaseSearchProgressActions().clearSearch(searchId);
    };
  }, [searchId, service, cacheDecision]);

  const loadCachedResults = useCallback(() => {
    setCacheDecision('use');
  }, []);

  const searchAgain = useCallback(() => {
    setCacheDecision('search');
  }, []);

  return {
    ...result,
    data: filteredData,
    selectedFilterKey,
    sortKey,
    sortDirection,
    searchId,
    totalItems,
    cacheStatus: cacheStatus.data,
    cacheDecision,
    loadCachedResults,
    searchAgain,
  };
};

export default useReleases;

export const useClearReleasesOnUnmount = (
  payload: InteractiveSearchPayload
) => {
  const queryClient = useQueryClient();
  const service: ServiceId = 'movieId' in payload ? 'movies' : 'series';

  useEffect(() => {
    return () => {
      // Remove by prefix: the query key also carries a per-search searchId param, so
      // matching on the exact payload would leave the cached search in place.
      queryClient.removeQueries({
        queryKey: getServiceQueryKey(service, '/release'),
      });
    };
  }, [service, queryClient]);
};

interface OverrideRelease {
  seriesId: number;
  episodeIds: number[];
  downloadClientId: number | null;
  quality: QualityModel;
  languages: Language[];
}

interface GrabRelease {
  guid: string;
  indexerId: number;
  override?: OverrideRelease;
  searchInfo?: InteractiveSearchPayload;
}

export interface MovieGrabRelease {
  guid: string;
  indexerId: number;
  movieId?: number;
  quality?: QualityModel;
  languages?: Language[];
  downloadClientId?: number | null;
  shouldOverride?: boolean;
}

export const useGrabRelease = (service: ServiceId = 'series') => {
  const [isGrabbed, setIsGrabbed] = useState(false);

  // Explicitly define the types for the mutation so we can pass in no arguments to mutate as expected.
  const { mutate, isPending, error } = useApiMutation<
    unknown,
    GrabRelease | MovieGrabRelease
  >({
    service,
    path: '/release',
    method: 'POST',
    mutationOptions: {
      onMutate: () => {
        setIsGrabbed(false);
      },
      onSuccess: () => {
        setIsGrabbed(true);
      },
    },
  });

  const grabError = useMemo(() => {
    if (!error) {
      return undefined;
    }

    return error.statusBody?.message ?? translate('InteractiveSearchGrabError');
  }, [error]);

  return {
    grabRelease: mutate,
    isGrabbing: isPending,
    isGrabbed,
    grabError,
  };
};

export const setReleaseSort = (
  sortKey: string,
  sortDirection: SortDirection | undefined
) => {
  releaseStore.setState((state) => applySort(state, sortKey, sortDirection));
};

const applyFilterPredicate = <T>(itemValue: T, value: T, type: FilterType) => {
  const predicate = getFilterTypePredicate(type);

  return predicate(itemValue, value);
};
