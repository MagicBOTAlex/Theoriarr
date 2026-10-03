import { useDebounce } from 'use-debounce';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface LibrarySearchResult {
  mediaType: 'series' | 'movie';
  id: number;
}

const EMPTY_RESULTS: LibrarySearchResult[] = [];

const useLibrarySearch = (term: string) => {
  const [debouncedTerm] = useDebounce(term.trim(), 250);

  const { data, isFetching, isFetched } = useApiQuery<LibrarySearchResult[]>({
    path: '/api/v3/library/search',
    queryParams: { term: debouncedTerm },
    queryOptions: {
      enabled: debouncedTerm.length > 0,
      staleTime: 5 * 60 * 1000,
      gcTime: Infinity,
    },
  });

  return {
    results: data ?? EMPTY_RESULTS,
    isSearching: debouncedTerm.length > 0 && (isFetching || !isFetched),
  };
};

export default useLibrarySearch;
