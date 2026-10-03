import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import ModelBase from 'App/ModelBase';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import usePage from 'Helpers/Hooks/usePage';
import usePagedApiQuery from 'Helpers/Hooks/usePagedApiQuery';
import { usePendingChangesStore } from 'Helpers/Hooks/usePendingChangesStore';
import { getServiceQueryKey, ServiceId } from 'Services';
import { useSettingsService } from 'Settings/SettingsServiceContext';
import selectSettings from 'Utilities/selectSettings';
import { useImportListExclusionOptions } from './importListExclusionOptionsStore';

// D6 — the merged exclusion store is MediaType-tagged. The series subsystem keeps the legacy
// `/importlistexclusion` route (TvdbId/Title) and the movies subsystem uses `/exclusions`
// (TmdbId/MovieTitle/MovieYear). One hook serves both.
export interface ImportListExclusion extends ModelBase {
  tvdbId: number;
  title: string;
  tmdbId: number;
  movieTitle: string;
  movieYear: number;
}

const SERIES_PATH = '/importlistexclusion';
const MOVIES_PATH = '/exclusions';

export function useImportListExclusionPath(service: ServiceId): string {
  return service === 'movies' ? MOVIES_PATH : SERIES_PATH;
}

const useImportListExclusions = () => {
  const service = useSettingsService();
  const path = useImportListExclusionPath(service);
  const { page, goToPage } = usePage('importListExclusion');
  const { pageSize, sortKey, sortDirection } = useImportListExclusionOptions();

  const { refetch, ...query } = usePagedApiQuery<ImportListExclusion>({
    path,
    service,
    page,
    pageSize,
    sortKey,
    sortDirection,
    queryOptions: {
      placeholderData: keepPreviousData,
    },
  });

  return {
    ...query,
    goToPage,
    page,
    refetch,
  };
};

export default useImportListExclusions;

interface ManageImportListExclusionOptions {
  id?: number;
  title?: string;
  tvdbId?: number;
  movieTitle?: string;
  tmdbId?: number;
  movieYear?: number;
}

export const useManageImportListExclusion = ({
  id,
  title,
  tvdbId,
  movieTitle,
  tmdbId,
  movieYear,
}: ManageImportListExclusionOptions) => {
  const service = useSettingsService();
  const path = useImportListExclusionPath(service);
  const queryClient = useQueryClient();

  const item = useMemo<ImportListExclusion>(() => {
    return {
      id: id ?? 0,
      tvdbId: tvdbId ?? 0,
      title: title ?? '',
      tmdbId: tmdbId ?? 0,
      movieTitle: movieTitle ?? '',
      movieYear: movieYear ?? 0,
    };
  }, [id, title, tvdbId, movieTitle, tmdbId, movieYear]);

  const { pendingChanges, setPendingChange } =
    usePendingChangesStore<ImportListExclusion>({});

  const {
    mutate,
    isPending: isSaving,
    error: saveError,
  } = useApiMutation<ImportListExclusion, ImportListExclusion>({
    path: id ? `${path}/${id}` : path,
    method: id ? 'PUT' : 'POST',
    service,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, path),
        });
      },
    },
  });

  const { settings, validationErrors, validationWarnings } = useMemo(() => {
    return selectSettings(item, pendingChanges, saveError);
  }, [item, pendingChanges, saveError]);

  const updateValue = useCallback(
    (name: string, value: unknown) => {
      // @ts-expect-error - name is not yet typed
      setPendingChange(name, value);
    },
    [setPendingChange]
  );

  const save = useCallback(() => {
    const payload = {
      ...item,
      ...pendingChanges,
    } as ImportListExclusion;

    if (id) {
      payload.id = id;
    }

    mutate(payload);
  }, [id, item, pendingChanges, mutate]);

  return {
    item,
    settings,
    isSaving,
    saveError,
    validationErrors,
    validationWarnings,
    updateValue,
    save,
  };
};

export const useDeleteImportListExclusion = (id: number) => {
  const service = useSettingsService();
  const path = useImportListExclusionPath(service);
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<unknown, void>({
    path: `${path}/${id}`,
    method: 'DELETE',
    service,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, path),
        });
      },
    },
  });

  return {
    deleteImportListExclusion: mutate,
    isDeleting: isPending,
  };
};

export const useDeleteImportListExclusions = () => {
  const service = useSettingsService();
  const path = useImportListExclusionPath(service);
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<
    unknown,
    BulkImportListExclusionData
  >({
    path: `${path}/bulk`,
    method: 'DELETE',
    service,
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({
          queryKey: getServiceQueryKey(service, path),
        });
      },
    },
  });

  return {
    deleteImportListExclusions: mutate,
    isDeleting: isPending,
  };
};

interface BulkImportListExclusionData {
  ids: number[];
}
