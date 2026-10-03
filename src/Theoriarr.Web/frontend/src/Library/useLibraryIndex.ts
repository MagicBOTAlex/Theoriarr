import { useMemo } from 'react';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import {
  LIBRARY_FILTER_PREDICATES,
  LIBRARY_FILTERS,
  LIBRARY_SORT_PREDICATES,
} from './libraryFilters';
import { useLibraryOptions } from './libraryOptionsStore';
import { MediaItem } from './MediaItem';
import useLibraryItems from './useLibraryItems';

export const useLibraryCustomFilters = () => {
  const seriesFilters = useCustomFiltersList('series');
  const movieFilters = useCustomFiltersList('movie');
  const libraryFilters = useCustomFiltersList('library');

  return useMemo(
    () => [...seriesFilters, ...movieFilters, ...libraryFilters],
    [seriesFilters, movieFilters, libraryFilters]
  );
};

const useLibraryIndex = () => {
  const { selectedFilterKey, sortKey, sortDirection } = useLibraryOptions();
  const { items, isLoading, isFetched, error } = useLibraryItems();
  const customFilters = useLibraryCustomFilters();

  const data = useMemo(() => {
    return clientSideFilterAndSort<
      MediaItem,
      typeof LIBRARY_FILTER_PREDICATES,
      typeof LIBRARY_SORT_PREDICATES
    >(items, {
      selectedFilterKey,
      filters: LIBRARY_FILTERS,
      filterPredicates: LIBRARY_FILTER_PREDICATES,
      customFilters,
      sortKey,
      sortDirection,
      secondarySortKey: 'sortTitle',
      secondarySortDirection: 'ascending',
      sortPredicates: LIBRARY_SORT_PREDICATES,
    });
  }, [customFilters, items, selectedFilterKey, sortKey, sortDirection]);

  return {
    data: data.data,
    totalItems: data.totalItems,
    isLoading,
    isFetched,
    isError: !!error,
    error,
  };
};

export default useLibraryIndex;
