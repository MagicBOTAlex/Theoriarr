import { useMemo } from 'react';
import usePagedApiQuery from 'Helpers/Hooks/usePagedApiQuery';
import Blocklist from 'typings/Blocklist';
import History from 'typings/History';
import Queue from 'typings/Queue';
import {
  blocklistToMediaBlocklistItem,
  historyToMediaHistoryItem,
  MediaBlocklistItem,
  MediaHistoryItem,
  MediaQueueItem,
  MovieBlocklistRecord,
  movieBlocklistToMediaBlocklistItem,
  MovieHistoryRecord,
  movieHistoryToMediaHistoryItem,
  MovieQueueRecord,
  movieQueueToMediaQueueItem,
  queueToMediaQueueItem,
} from './mediaActivity';

const FETCH_ALL = 100000;

export const useMediaQueue = () => {
  const series = usePagedApiQuery<Queue>({
    service: 'series',
    path: '/queue',
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { includeSubresources: ['Series', 'Episodes'] },
  });

  const movies = usePagedApiQuery<MovieQueueRecord>({
    service: 'movies',
    path: '/queue',
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { includeMovie: true },
  });

  const items = useMemo(() => {
    return [
      ...series.records.map(queueToMediaQueueItem),
      ...movies.records.map(movieQueueToMediaQueueItem),
    ];
  }, [series.records, movies.records]);

  return {
    items,
    isLoading: series.isLoading || movies.isLoading,
    error: series.error ?? movies.error,
  };
};

export const useMediaHistory = () => {
  const series = usePagedApiQuery<History>({
    service: 'series',
    path: '/history',
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { includeSeries: true, includeEpisode: true },
  });

  const movies = usePagedApiQuery<MovieHistoryRecord>({
    service: 'movies',
    path: '/history',
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { includeMovie: true },
  });

  const items = useMemo(() => {
    return [
      ...series.records.map(historyToMediaHistoryItem),
      ...movies.records.map(movieHistoryToMediaHistoryItem),
    ];
  }, [series.records, movies.records]);

  return {
    items,
    isLoading: series.isLoading || movies.isLoading,
    error: series.error ?? movies.error,
  };
};

export const useMediaBlocklist = () => {
  const series = usePagedApiQuery<Blocklist>({
    service: 'series',
    path: '/blocklist',
    page: 1,
    pageSize: FETCH_ALL,
  });

  const movies = usePagedApiQuery<MovieBlocklistRecord>({
    service: 'movies',
    path: '/blocklist',
    page: 1,
    pageSize: FETCH_ALL,
  });

  const items = useMemo(() => {
    return [
      ...series.records.map(blocklistToMediaBlocklistItem),
      ...movies.records.map(movieBlocklistToMediaBlocklistItem),
    ];
  }, [series.records, movies.records]);

  return {
    items,
    isLoading: series.isLoading || movies.isLoading,
    error: series.error ?? movies.error,
  };
};

export type { MediaBlocklistItem, MediaHistoryItem, MediaQueueItem };
