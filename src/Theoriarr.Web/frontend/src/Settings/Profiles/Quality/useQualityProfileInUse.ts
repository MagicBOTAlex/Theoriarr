import { useMemo } from 'react';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import useSeries from 'Series/useSeries';
import { useImportListsData } from 'Settings/ImportLists/ImportLists/useImportLists';
import { useSettingsService } from 'Settings/SettingsServiceContext';

interface QualityProfileUsageItem {
  qualityProfileId: number;
}

function useQualityProfileInUse(id: number | undefined) {
  const service = useSettingsService();
  const { data: series = [] } = useSeries();
  const { data: movies = [] } = useApiQuery<QualityProfileUsageItem[]>({
    service: 'movies',
    path: '/movie',
    queryOptions: {
      enabled: service === 'movies',
    },
  });
  const importLists = useImportListsData();

  return useMemo(() => {
    if (!id) {
      return {
        seriesCount: 0,
        importListCount: 0,
      };
    }

    const inUseItems = service === 'movies' ? movies : series;

    return {
      seriesCount: inUseItems.filter((item) => item.qualityProfileId === id)
        .length,
      importListCount: importLists.filter(
        (list) => list.qualityProfileId === id
      ).length,
    };
  }, [id, series, movies, service, importLists]);
}

export default useQualityProfileInUse;
