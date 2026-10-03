import { useMemo } from 'react';
import Episode from 'Episode/Episode';
import usePagedApiQuery from 'Helpers/Hooks/usePagedApiQuery';
import { Movie } from 'Movies/Movie';
import {
  episodeToMediaWantedItem,
  MediaWantedItem,
  movieToMediaWantedItem,
} from './mediaWanted';

export type WantedTab = 'missing' | 'cutoff';

// The per-domain wanted endpoints require an explicit `monitored` flag, so the
// unified page requests both and merges them (deduped by key) to show
// monitored and unmonitored rows together.
const FETCH_ALL = 100000;

const useMediaWanted = (tab: WantedTab) => {
  const path = tab === 'missing' ? '/wanted/missing' : '/wanted/cutoff';

  const seriesMonitored = usePagedApiQuery<Episode>({
    service: 'series',
    path,
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: {
      includeSeries: true,
      includeEpisodeFile: true,
      monitored: true,
    },
  });

  const seriesUnmonitored = usePagedApiQuery<Episode>({
    service: 'series',
    path,
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: {
      includeSeries: true,
      includeEpisodeFile: true,
      monitored: false,
    },
  });

  const moviesMonitored = usePagedApiQuery<Movie>({
    service: 'movies',
    path,
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { monitored: true },
  });

  const moviesUnmonitored = usePagedApiQuery<Movie>({
    service: 'movies',
    path,
    page: 1,
    pageSize: FETCH_ALL,
    queryParams: { monitored: false },
  });

  const items = useMemo(() => {
    const byKey = new Map<string, MediaWantedItem>();

    [...seriesMonitored.records, ...seriesUnmonitored.records].forEach(
      (episode) => {
        const item = episodeToMediaWantedItem(episode);
        byKey.set(item.key, item);
      }
    );

    [...moviesMonitored.records, ...moviesUnmonitored.records].forEach(
      (movie) => {
        const item = movieToMediaWantedItem(movie);
        byKey.set(item.key, item);
      }
    );

    return Array.from(byKey.values());
  }, [
    seriesMonitored.records,
    seriesUnmonitored.records,
    moviesMonitored.records,
    moviesUnmonitored.records,
  ]);

  return {
    items,
    isLoading:
      seriesMonitored.isLoading ||
      seriesUnmonitored.isLoading ||
      moviesMonitored.isLoading ||
      moviesUnmonitored.isLoading,
    error:
      seriesMonitored.error ??
      seriesUnmonitored.error ??
      moviesMonitored.error ??
      moviesUnmonitored.error,
  };
};

export default useMediaWanted;
