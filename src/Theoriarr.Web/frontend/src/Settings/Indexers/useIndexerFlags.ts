import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { useSettingsService } from 'Settings/SettingsServiceContext';

export interface IndexerFlag {
  id: number;
  name: string;
}

const DEFAULT_INDEXER_FLAGS: IndexerFlag[] = [];

const useIndexerFlags = () => {
  const service = useSettingsService();
  const result = useApiQuery<IndexerFlag[]>({
    path: '/indexerFlag',
    service,
    queryOptions: {
      gcTime: Infinity,
      staleTime: Infinity,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_INDEXER_FLAGS,
  };
};

export default useIndexerFlags;
