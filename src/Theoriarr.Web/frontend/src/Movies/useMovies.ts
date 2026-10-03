import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { getServiceQueryKey } from 'Services';
import { Movie } from './Movie';

const DEFAULT_MOVIES: Movie[] = [];

const useMovies = () => {
  const { data, ...rest } = useApiQuery<Movie[]>({
    service: 'movies',
    path: '/movie',
    queryOptions: {
      staleTime: 5 * 60 * 1000,
      gcTime: Infinity,
    },
  });

  const movieMap = useMemo(() => {
    if (!data) {
      return new Map<number, Movie>();
    }

    return new Map<number, Movie>(data.map((movie) => [movie.id, movie]));
  }, [data]);

  return {
    ...rest,
    data: data ?? DEFAULT_MOVIES,
    movieMap,
  };
};

export default useMovies;

export const useMovieMap = () => {
  const { movieMap } = useMovies();

  return movieMap;
};

export const useSingleMovie = (movieId?: number) => {
  const movieMap = useMovieMap();

  return useMemo(() => {
    if (!movieId) {
      return undefined;
    }

    return movieMap.get(movieId);
  }, [movieMap, movieId]);
};

interface SaveMoviePayload extends Partial<Movie> {
  id: number;
  moveFiles?: boolean;
}

interface DeleteMoviePayload {
  deleteFiles?: boolean;
  addImportExclusion?: boolean;
}

interface ToggleMovieMonitoredPayload {
  monitored: boolean;
}

export const useSaveMovie = (moveFiles?: boolean) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<Movie, SaveMoviePayload>({
    service: 'movies',
    path: '/movie',
    queryParams: {
      moveFiles,
    },
    method: 'PUT',
    mutationOptions: {
      onSuccess: (updatedMovie) => {
        queryClient.setQueryData<Movie[]>(
          getServiceQueryKey('movies', '/movie'),
          (oldMovies) => {
            if (!oldMovies) {
              return oldMovies;
            }

            return oldMovies.map((movie) => {
              if (movie.id === updatedMovie.id) {
                return {
                  ...movie,
                  ...updatedMovie,
                };
              }

              return movie;
            });
          }
        );
      },
    },
  });

  return {
    saveMovie: mutate,
    isSaving: isPending,
    saveError: error,
  };
};

export const useDeleteMovie = (
  movieId: number,
  options: DeleteMoviePayload
) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<unknown, void>({
    service: 'movies',
    path: `/movie/${movieId}`,
    queryParams: {
      ...options,
    },
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.setQueryData<Movie[]>(
          getServiceQueryKey('movies', '/movie'),
          (oldMovies) => {
            if (!oldMovies) {
              return oldMovies;
            }

            return oldMovies.filter((movie) => movie.id !== movieId);
          }
        );
      },
    },
  });

  return {
    deleteMovie: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

export const useToggleMovieMonitored = (movieId: number) => {
  const queryClient = useQueryClient();
  const movie = useSingleMovie(movieId);

  const { mutate, isPending, error } = useApiMutation<
    Movie,
    ToggleMovieMonitoredPayload
  >({
    service: 'movies',
    path: '/movie',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (updatedMovie) => {
        queryClient.setQueryData<Movie[]>(
          getServiceQueryKey('movies', '/movie'),
          (oldMovies) => {
            if (!oldMovies) {
              return oldMovies;
            }

            return oldMovies.map((oldMovie) =>
              oldMovie.id === updatedMovie.id ? updatedMovie : oldMovie
            );
          }
        );
      },
    },
  });

  const toggleMovieMonitored = useCallback(
    (payload: ToggleMovieMonitoredPayload) => {
      return mutate({ ...movie, ...payload });
    },
    [movie, mutate]
  );

  return {
    toggleMovieMonitored,
    isTogglingMovieMonitored: isPending,
    toggleMovieMonitoredError: error,
  };
};
