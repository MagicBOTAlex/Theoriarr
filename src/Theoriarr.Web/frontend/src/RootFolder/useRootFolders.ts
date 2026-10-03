import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import ModelBase from 'App/ModelBase';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { getServiceQueryKey, ServiceId } from 'Services';
import { useSettingsService } from 'Settings/SettingsServiceContext';

export interface UnmappedFolder {
  name: string;
  path: string;
  relativePath: string;
}

// Stored discriminator: "series" is shown as TV, Anime is a series-domain type.
export type RootFolderMediaType = 'series' | 'movie' | 'anime';

export interface RootFolder extends ModelBase {
  id: number;
  path: string;
  mediaType: RootFolderMediaType;
  accessible: boolean;
  isEmpty: boolean;
  freeSpace?: number;
  unmappedFolders: UnmappedFolder[];
}

interface AddRootFolder {
  path: string;
  mediaType: RootFolderMediaType;
}

interface UpdateRootFolder {
  path: string;
  mediaType: RootFolderMediaType;
}

// TV and Anime folders are created through the Series key, Movies through the
// Movies key, so the create response is visible to the key that made it.
export function serviceForMediaType(mediaType: RootFolderMediaType): ServiceId {
  return mediaType === 'movie' ? 'movies' : 'series';
}

const DEFAULT_ROOT_FOLDERS: RootFolder[] = [];

const useRootFolders = (all = false) => {
  const service = useSettingsService();
  const result = useApiQuery<RootFolder[]>({
    path: '/rootFolder',
    service,
    // The single Media Management page passes `all` so it lists TV, Anime and
    // Movies folders together; `all=true` skips the per-key domain scoping.
    queryParams: all ? { all: true } : undefined,
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_ROOT_FOLDERS,
  };
};

export const useRootFolder = (id: number, timeout: boolean) => {
  const service = useSettingsService();
  const result = useApiQuery<RootFolder>({
    path: `/rootFolder/${id}`,
    service,
    queryParams: { timeout },
    queryOptions: {
      // Disable refetch on window focus to prevent refetching when the user switch tabs
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data,
  };
};

export default useRootFolders;

export const useDeleteRootFolder = (
  id: number,
  service: ServiceId = 'series'
) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<unknown, void>({
    path: `/rootFolder/${id}`,
    method: 'DELETE',
    service,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('series', '/rootFolder'),
        });
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/rootFolder'),
        });
      },
    },
  });

  return {
    deleteRootFolder: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

export const useUpdateRootFolder = (
  id: number,
  service: ServiceId = 'series'
) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    RootFolder,
    UpdateRootFolder
  >({
    path: `/rootFolder/${id}`,
    method: 'PUT',
    service,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('series', '/rootFolder'),
        });
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/rootFolder'),
        });
      },
    },
  });

  return {
    updateRootFolder: mutate,
    isUpdating: isPending,
    updateError: error,
  };
};

export const useAddRootFolder = () => {
  const queryClient = useQueryClient();

  const invalidate = useCallback(() => {
    queryClient.invalidateQueries({
      queryKey: getServiceQueryKey('series', '/rootFolder'),
    });
    queryClient.invalidateQueries({
      queryKey: getServiceQueryKey('movies', '/rootFolder'),
    });
  }, [queryClient]);

  const seriesMutation = useApiMutation<RootFolder, AddRootFolder>({
    path: '/rootFolder',
    method: 'POST',
    service: 'series',
    mutationOptions: { onSuccess: invalidate },
  });

  const moviesMutation = useApiMutation<RootFolder, AddRootFolder>({
    path: '/rootFolder',
    method: 'POST',
    service: 'movies',
    mutationOptions: { onSuccess: invalidate },
  });

  const addRootFolder = useCallback(
    (payload: AddRootFolder) => {
      const mutation =
        payload.mediaType === 'movie' ? moviesMutation : seriesMutation;

      mutation.mutate(payload);
    },
    [seriesMutation, moviesMutation]
  );

  return {
    addRootFolder,
    isAdding: seriesMutation.isPending || moviesMutation.isPending,
    addError: seriesMutation.error ?? moviesMutation.error,
    newRootFolder: seriesMutation.data ?? moviesMutation.data,
  };
};
