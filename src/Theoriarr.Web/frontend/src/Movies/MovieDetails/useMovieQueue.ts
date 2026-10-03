import usePagedApiQuery from 'Helpers/Hooks/usePagedApiQuery';
import { sortDirections } from 'Helpers/Props';
import Queue from 'typings/Queue';

const useMovieQueue = (movieId: number) => {
  const { records } = usePagedApiQuery<Queue>({
    service: 'movies',
    path: '/queue',
    queryParams: { movieIds: movieId },
    page: 1,
    pageSize: 1,
    sortKey: 'timeleft',
    sortDirection: sortDirections.ASCENDING,
  });

  return records[0] ?? null;
};

export default useMovieQueue;
