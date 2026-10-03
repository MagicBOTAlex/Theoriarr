import useApiQuery from 'Helpers/Hooks/useApiQuery';
import MovieCredit from 'typings/MovieCredit';

const DEFAULT_MOVIE_CREDITS: MovieCredit[] = [];

const useMovieCredits = (movieId?: number) => {
  const { data, ...result } = useApiQuery<MovieCredit[]>({
    service: 'movies',
    path: '/credit',
    queryParams: { movieId },
    queryOptions: {
      enabled: movieId !== undefined,
    },
  });

  return {
    ...result,
    data: data ?? DEFAULT_MOVIE_CREDITS,
  };
};

export default useMovieCredits;
