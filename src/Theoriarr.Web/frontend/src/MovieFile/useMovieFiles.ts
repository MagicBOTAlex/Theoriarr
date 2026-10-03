import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { getServiceQueryKey } from 'Services';
import { MovieFile } from './MovieFile';

const DEFAULT_MOVIE_FILES: MovieFile[] = [];

interface MovieFileFilter {
  movieId: number;
}

interface DeleteMovieFilesPayload {
  movieFileIds: number[];
}

const useMovieFiles = (params: MovieFileFilter) => {
  const result = useApiQuery<MovieFile[]>({
    service: 'movies',
    path: '/moviefile',
    queryParams: { ...params },
    queryOptions: {
      enabled: params.movieId !== undefined,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_MOVIE_FILES,
    hasMovieFiles: !!result.data?.length,
  };
};

export default useMovieFiles;

export const useDeleteMovieFiles = () => {
  const queryClient = useQueryClient();

  const { mutate, error, isPending } = useApiMutation<
    unknown,
    DeleteMovieFilesPayload
  >({
    service: 'movies',
    method: 'DELETE',
    path: '/moviefile/bulk',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/moviefile'),
        });
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/movie'),
        });
      },
    },
  });

  return {
    deleteMovieFiles: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};
