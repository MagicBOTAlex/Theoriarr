import { useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { MonitorNewItems, SeriesMonitor, SeriesType } from 'Series/Series';
import {
  SelectedSchema,
  useProviderSchema,
  useSelectedSchema,
} from 'Settings/useProviderSchema';
import {
  useDeleteProvider,
  useManageProviderSettings,
  useProviderSettings,
} from 'Settings/useProviderSettings';
import Provider from 'typings/Provider';
import { getServiceQueryKey } from 'Services';
import { ApiError } from 'Utilities/Fetch/fetchJson';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import translate from 'Utilities/String/translate';
import { useSettingsService } from 'Settings/SettingsServiceContext';

export interface ImportListModel extends Provider {
  enableAutomaticAdd: boolean;
  searchForMissingEpisodes: boolean;
  shouldMonitor: SeriesMonitor;
  monitorNewItems: MonitorNewItems;
  rootFolderPath: string;
  qualityProfileId: number;
  seriesType: SeriesType;
  seasonFolder: boolean;
  listType: string;
  listOrder: number;
  minRefreshInterval: string;
  tags: number[];
  tagExisting: boolean;
  monitor: string;
  minimumAvailability: string;
  searchOnAdd: boolean;
  enableAuto: boolean;
}

interface BulkEditImportListsPayload {
  ids: number[];
  [key: string]: unknown;
}

interface BulkDeleteImportListsPayload {
  ids: number[];
}

const PATH = '/importlist';

export const useImportLists = () => {
  return useProviderSettings<ImportListModel>({
    path: PATH,
  });
};

export const useImportListsData = () => {
  const { data } = useImportLists();

  return data;
};

const SORT_PREDICATES = {
  name: (item: ImportListModel) => item.name.toLowerCase(),
};

export const useSortedImportLists = (
  sortKey: keyof ImportListModel = 'name',
  sortDirection: SortDirection = 'ascending'
) => {
  const result = useImportLists();

  const sortedData = useMemo(
    () =>
      clientSideFilterAndSort<ImportListModel, never, typeof SORT_PREDICATES>(
        result.data,
        {
          sortKey,
          sortDirection,
          secondarySortKey: 'name',
          secondarySortDirection: 'ascending',
          sortPredicates: SORT_PREDICATES,
        }
      ).data,
    [result.data, sortKey, sortDirection]
  );

  return {
    ...result,
    data: sortedData,
  };
};

export const useImportList = (id: number | undefined) => {
  const { data } = useImportLists();

  if (id === undefined) {
    return undefined;
  }

  return data.find((list) => list.id === id);
};

export const useImportListsWithIds = (ids: number[]) => {
  const allImportLists = useImportListsData();

  return allImportLists.filter((list) => ids.includes(list.id));
};

export const useManageImportList = (
  id: number | undefined,
  cloneId: number | undefined,
  selectedSchema?: SelectedSchema
) => {
  const schema = useSelectedSchema<ImportListModel>(PATH, selectedSchema);
  const { schema: allSchemas } = useImportListSchema(selectedSchema != null);
  const cloneImportList = useImportList(cloneId);

  if (cloneId && !cloneImportList) {
    throw new Error(`ImportList with ID ${cloneId} not found`);
  }

  if (selectedSchema && !schema) {
    throw new Error('A selected schema is required to manage import list');
  }

  const defaultProvider = useMemo(() => {
    if (cloneId && cloneImportList) {
      const clonedImportList = {
        ...cloneImportList,
        id: 0,
        name: translate('DefaultNameCopiedImportList', {
          name: cloneImportList.name,
        }),
      };

      clonedImportList.fields = clonedImportList.fields.map((field) => {
        const newField = { ...field };

        if (newField.privacy === 'apiKey' || newField.privacy === 'password') {
          newField.value = '';
        }

        return newField;
      });

      return clonedImportList;
    }

    if (selectedSchema && schema) {
      // Presets are returned by the backend without provider characteristics
      // (minRefreshInterval is TimeSpan.Zero), so inherit it from the parent
      // implementation when a preset is selected.
      const parent = selectedSchema.presetName
        ? allSchemas.find(
            (s) => s.implementation === selectedSchema.implementation
          )
        : undefined;

      return {
        ...schema,
        minRefreshInterval:
          parent?.minRefreshInterval ?? schema.minRefreshInterval,
        name: selectedSchema.presetName ?? schema.implementationName,
        enableAutomaticAdd: true,
        shouldMonitor: 'all' as SeriesMonitor,
        seriesType: 'standard' as SeriesType,
        seasonFolder: true,
        rootFolderPath: '',
      };
    }

    return {} as ImportListModel;
  }, [cloneId, cloneImportList, schema, selectedSchema, allSchemas]);

  const manage = useManageProviderSettings<ImportListModel>(
    id,
    defaultProvider,
    PATH
  );

  return manage;
};

export const useDeleteImportList = (id: number) => {
  const result = useDeleteProvider<ImportListModel>(id, PATH);

  return {
    ...result,
    deleteImportList: result.deleteProvider,
  };
};

export const useImportListSchema = (enabled: boolean = true) => {
  return useProviderSchema<ImportListModel>(PATH, enabled);
};

export const useTestAllImportLists = (
  onSuccess?: () => void,
  onError?: (error: ApiError) => void
) => {
  const service = useSettingsService();
  const { mutate, isPending, error } = useApiMutation<void, void>({
    path: `${PATH}/testall`,
    method: 'POST',
    service,
    mutationOptions: {
      onSuccess,
      onError,
    },
  });

  return {
    testAllImportLists: mutate,
    isTestingAllImportLists: isPending,
    testAllError: error,
  };
};

export const useBulkEditImportLists = (
  onSuccess?: () => void,
  onError?: (error: ApiError) => void
) => {
  const service = useSettingsService();
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    ImportListModel[],
    BulkEditImportListsPayload
  >({
    path: `${PATH}/bulk`,
    method: 'PUT',
    service,
    mutationOptions: {
      onSuccess: (updatedImportLists) => {
        queryClient.setQueryData<ImportListModel[]>(
          getServiceQueryKey(service, PATH),
          (oldImportLists) => {
            if (!oldImportLists) {
              return oldImportLists;
            }

            return oldImportLists.map((list) => {
              const updated = updatedImportLists.find((u) => u.id === list.id);

              return updated ? { ...list, ...updated } : list;
            });
          }
        );
        onSuccess?.();
      },
      onError,
    },
  });

  return {
    bulkEditImportLists: mutate,
    isSaving: isPending,
    bulkError: error,
  };
};

export const useBulkDeleteImportLists = (
  onSuccess?: () => void,
  onError?: (error: ApiError) => void
) => {
  const service = useSettingsService();
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    void,
    BulkDeleteImportListsPayload
  >({
    path: `${PATH}/bulk`,
    method: 'DELETE',
    service,
    mutationOptions: {
      onSuccess: (_, variables) => {
        const deletedIds = new Set(variables.ids);

        queryClient.setQueryData<ImportListModel[]>(
          getServiceQueryKey(service, PATH),
          (oldImportLists) => {
            if (!oldImportLists) {
              return oldImportLists;
            }

            return oldImportLists.filter((list) => !deletedIds.has(list.id));
          }
        );
        onSuccess?.();
      },
      onError,
    },
  });

  return {
    bulkDeleteImportLists: mutate,
    isDeleting: isPending,
    bulkDeleteError: error,
  };
};
