import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { Movie } from './Movie';
import useMovies from './useMovies';

export const MOVIES_QUERY_KEY = ['movies', '/movie'];

export interface ToggleMovieMonitoredPayload {
  monitored: boolean;
}

export interface SaveMovieEditorPayload {
  movieIds: number[];
  monitored?: boolean;
  qualityProfileId?: number;
  minimumAvailability?: string;
  rootFolderPath?: string;
  tags?: number[];
  applyTags?: string;
  moveFiles?: boolean;
}

export interface BulkDeleteMoviesPayload {
  movieIds: number[];
  deleteFiles?: boolean;
  addImportExclusion?: boolean;
}

export const useSingleMovie = (movieId?: number) => {
  const { data } = useMovies();

  return useMemo(() => {
    if (!movieId) {
      return undefined;
    }

    return data.find((movie) => movie.id === movieId);
  }, [data, movieId]);
};

export const useMultipleMovies = (movieIds: number[]) => {
  const { data } = useMovies();

  return useMemo(() => {
    if (movieIds.length === 0) {
      return [];
    }

    return movieIds.reduce<Movie[]>((acc, movieId) => {
      const movie = data.find((m) => m.id === movieId);

      if (movie) {
        acc.push(movie);
      }

      return acc;
    }, []);
  }, [data, movieIds]);
};

export const useToggleMovieMonitored = (movieId: number) => {
  const queryClient = useQueryClient();
  const movie = useSingleMovie(movieId);

  const { mutate, isPending, error } = useApiMutation<Movie, Movie>({
    service: 'movies',
    path: '/movie',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (updatedMovie) => {
        queryClient.setQueryData<Movie[]>(MOVIES_QUERY_KEY, (oldMovies) => {
          if (!oldMovies) {
            return oldMovies;
          }

          return oldMovies.map((m) =>
            m.id === updatedMovie.id ? updatedMovie : m
          );
        });
      },
    },
  });

  const toggleMovieMonitored = useCallback(
    (payload: ToggleMovieMonitoredPayload) => {
      if (!movie) {
        return;
      }

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

export const useSaveMovieEditor = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    Movie[],
    SaveMovieEditorPayload
  >({
    service: 'movies',
    path: '/movie/editor',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (updatedMovies) => {
        queryClient.setQueryData<Movie[]>(MOVIES_QUERY_KEY, (oldMovies) => {
          if (!oldMovies) {
            return oldMovies;
          }

          return oldMovies.map((movie) => {
            const updated = updatedMovies.find((m) => m.id === movie.id);

            if (updated) {
              return { ...movie, ...updated };
            }

            return movie;
          });
        });
      },
    },
  });

  return {
    saveMovieEditor: mutate,
    isSavingMovieEditor: isPending,
    saveMovieEditorError: error,
  };
};

export const useBulkDeleteMovies = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    void,
    BulkDeleteMoviesPayload
  >({
    service: 'movies',
    path: '/movie/editor',
    method: 'DELETE',
    mutationOptions: {
      onSuccess: (_, variables) => {
        const movieIds = new Set(variables.movieIds);

        queryClient.setQueryData<Movie[]>(MOVIES_QUERY_KEY, (oldMovies) => {
          if (!oldMovies) {
            return oldMovies;
          }

          return oldMovies.filter((movie) => !movieIds.has(movie.id));
        });
      },
    },
  });

  return {
    bulkDeleteMovies: mutate,
    isBulkDeletingMovies: isPending,
    bulkDeleteMoviesError: error,
  };
};
