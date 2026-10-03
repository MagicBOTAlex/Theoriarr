import { useMemo } from 'react';
import useMovies from 'Movies/useMovies';
import useSeries from 'Series/useSeries';
import { movieToMediaItem, seriesToMediaItem } from './MediaItem';

const useLibraryItems = () => {
  const series = useSeries();
  const movies = useMovies();

  const items = useMemo(() => {
    return [
      ...series.data.map(seriesToMediaItem),
      ...movies.data.map(movieToMediaItem),
    ];
  }, [series.data, movies.data]);

  return {
    items,
    isLoading: series.isLoading || movies.isLoading,
    isFetched: series.isFetched && movies.isFetched,
    error: series.error ?? movies.error,
  };
};

export default useLibraryItems;
