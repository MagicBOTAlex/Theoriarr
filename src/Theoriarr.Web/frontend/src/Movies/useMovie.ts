import { useSingleMovie } from './useMovies';

function useMovie(movieId: number | undefined) {
  return useSingleMovie(movieId);
}

export default useMovie;
