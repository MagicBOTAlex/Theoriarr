import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import { useQueueOption } from 'Activity/Queue/queueOptionsStore';
import Command, { NewCommandBody } from 'Commands/Command';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { getServiceQueryKey, ServiceId } from 'Services';
import { MediaActivityType } from './mediaActivity';

function toService(type: MediaActivityType): ServiceId {
  return type === 'movie' ? 'movies' : 'series';
}

export interface MediaActivityRef {
  type: MediaActivityType;
  id: number;
}

interface BulkData {
  ids: number[];
}

// The removal options live in the shared queue options store which the reused
// `RemoveQueueItemModal` also edits, so both single and bulk removal honour the
// method the user picked in the confirmation modal.
const useRemovalOptions = () => {
  const removalOptions = useQueueOption('removalOptions');

  return useMemo(
    () => ({
      removeFromClient: removalOptions.removalMethod === 'removeFromClient',
      changeCategory: removalOptions.removalMethod === 'changeCategory',
      blocklist: removalOptions.blocklistMethod !== 'doNotBlocklist',
      skipRedownload: removalOptions.blocklistMethod === 'blocklistOnly',
    }),
    [removalOptions]
  );
};

const partitionByService = (items: MediaActivityRef[]) => {
  return {
    seriesIds: items
      .filter((item) => item.type === 'series')
      .map((item) => item.id),
    movieIds: items
      .filter((item) => item.type === 'movie')
      .map((item) => item.id),
  };
};

export const useRemoveMediaQueueItem = (
  type: MediaActivityType,
  id: number
) => {
  const queryClient = useQueryClient();
  const service = toService(type);
  const removalOptions = useRemovalOptions();

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/queue/${id}`,
    method: 'DELETE',
    queryParams: removalOptions,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/queue'),
        });
      },
    },
  });

  return { removeQueueItem: mutate, isRemoving: isPending };
};

export const useBulkRemoveMediaQueueItems = () => {
  const queryClient = useQueryClient();
  const removalOptions = useRemovalOptions();

  const seriesMutation = useApiMutation<unknown, BulkData>({
    service: 'series',
    path: '/queue/bulk',
    method: 'DELETE',
    queryParams: removalOptions,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('series', '/queue'),
        });
      },
    },
  });

  const moviesMutation = useApiMutation<unknown, BulkData>({
    service: 'movies',
    path: '/queue/bulk',
    method: 'DELETE',
    queryParams: removalOptions,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/queue'),
        });
      },
    },
  });

  const removeQueueItems = useCallback(
    (items: MediaActivityRef[]) => {
      const { seriesIds, movieIds } = partitionByService(items);

      if (seriesIds.length) {
        seriesMutation.mutate({ ids: seriesIds });
      }

      if (movieIds.length) {
        moviesMutation.mutate({ ids: movieIds });
      }
    },
    [seriesMutation, moviesMutation]
  );

  return {
    removeQueueItems,
    isRemoving: seriesMutation.isPending || moviesMutation.isPending,
  };
};

export const useGrabMediaQueueItem = (type: MediaActivityType, id: number) => {
  const queryClient = useQueryClient();
  const service = toService(type);

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/queue/grab/${id}`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/queue'),
        });
      },
    },
  });

  return { grabQueueItem: mutate, isGrabbing: isPending };
};

export const useFixMediaQueuePath = (type: MediaActivityType, id: number) => {
  const queryClient = useQueryClient();
  const service = toService(type);

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/queue/fixpath/${id}`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/queue'),
        });
      },
    },
  });

  return { fixQueuePath: mutate, isFixingPath: isPending };
};

// Delete a corrupt download from the client, blocklist the release and search again.
export const useRedownloadMediaQueueItem = (
  type: MediaActivityType,
  id: number
) => {
  const queryClient = useQueryClient();
  const service = toService(type);

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/queue/${id}`,
    method: 'DELETE',
    queryParams: {
      removeFromClient: true,
      blocklist: true,
      skipRedownload: false,
    },
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/queue'),
        });
      },
    },
  });

  return { redownloadQueueItem: mutate, isRedownloading: isPending };
};

export const useBulkGrabMediaQueueItems = () => {
  const queryClient = useQueryClient();

  const seriesMutation = useApiMutation<unknown, BulkData>({
    service: 'series',
    path: '/queue/grab/bulk',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('series', '/queue'),
        });
      },
    },
  });

  const moviesMutation = useApiMutation<unknown, BulkData>({
    service: 'movies',
    path: '/queue/grab/bulk',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/queue'),
        });
      },
    },
  });

  const grabQueueItems = useCallback(
    (items: MediaActivityRef[]) => {
      const { seriesIds, movieIds } = partitionByService(items);

      if (seriesIds.length) {
        seriesMutation.mutate({ ids: seriesIds });
      }

      if (movieIds.length) {
        moviesMutation.mutate({ ids: movieIds });
      }
    },
    [seriesMutation, moviesMutation]
  );

  return {
    grabQueueItems,
    isGrabbing: seriesMutation.isPending || moviesMutation.isPending,
  };
};

export const useMarkMediaHistoryFailed = (
  type: MediaActivityType,
  id: number
) => {
  const queryClient = useQueryClient();
  const service = toService(type);

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/history/failed/${id}`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/history'),
        });
      },
    },
  });

  return { markAsFailed: mutate, isMarkingAsFailed: isPending };
};

export const useRemoveMediaBlocklistItem = (
  type: MediaActivityType,
  id: number
) => {
  const queryClient = useQueryClient();
  const service = toService(type);

  const { mutate, isPending } = useApiMutation<unknown, void>({
    service,
    path: `/blocklist/${id}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, '/blocklist'),
        });
      },
    },
  });

  return { removeBlocklistItem: mutate, isRemoving: isPending };
};

export const useBulkRemoveMediaBlocklistItems = () => {
  const queryClient = useQueryClient();

  const seriesMutation = useApiMutation<unknown, BulkData>({
    service: 'series',
    path: '/blocklist/bulk',
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('series', '/blocklist'),
        });
      },
    },
  });

  const moviesMutation = useApiMutation<unknown, BulkData>({
    service: 'movies',
    path: '/blocklist/bulk',
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey('movies', '/blocklist'),
        });
      },
    },
  });

  const removeBlocklistItems = useCallback(
    (items: MediaActivityRef[]) => {
      const { seriesIds, movieIds } = partitionByService(items);

      if (seriesIds.length) {
        seriesMutation.mutate({ ids: seriesIds });
      }

      if (movieIds.length) {
        moviesMutation.mutate({ ids: movieIds });
      }
    },
    [seriesMutation, moviesMutation]
  );

  return {
    removeBlocklistItems,
    isRemoving: seriesMutation.isPending || moviesMutation.isPending,
  };
};

const CLEAR_BLOCKLIST_COMMAND: NewCommandBody = { name: 'ClearBlocklist' };

// Clears the whole blocklist for both domains. Unlike "remove selected" (which
// uses the bulk endpoint) this mirrors Sonarr's Clear Blocklist toolbar action
// via the ClearBlocklist command.
export const useClearMediaBlocklist = () => {
  const queryClient = useQueryClient();

  const invalidate = useCallback(
    (service: ServiceId) => {
      queryClient.invalidateQueries({
        queryKey: getServiceQueryKey(service, '/blocklist'),
      });
    },
    [queryClient]
  );

  const seriesMutation = useApiMutation<Command, NewCommandBody>({
    service: 'series',
    path: '/command',
    method: 'POST',
    mutationOptions: { onSuccess: () => invalidate('series') },
  });

  const moviesMutation = useApiMutation<Command, NewCommandBody>({
    service: 'movies',
    path: '/command',
    method: 'POST',
    mutationOptions: { onSuccess: () => invalidate('movies') },
  });

  const clearBlocklist = useCallback(() => {
    seriesMutation.mutate(CLEAR_BLOCKLIST_COMMAND);
    moviesMutation.mutate(CLEAR_BLOCKLIST_COMMAND);
  }, [seriesMutation, moviesMutation]);

  return {
    clearBlocklist,
    isClearing: seriesMutation.isPending || moviesMutation.isPending,
  };
};
