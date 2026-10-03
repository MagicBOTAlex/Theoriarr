import { useSettingsService } from 'Settings/SettingsServiceContext';
import { useManageSettings, useSettings } from 'Settings/useSettings';

export interface DownloadClientSettingsModel {
  downloadClientWorkingFolders: string;
  enableCompletedDownloadHandling: boolean;
  autoRedownloadFailed: boolean;
  autoRedownloadFailedFromInteractiveSearch: boolean;
}

// The series service talks to the v5 API (settings/*) while the movies service talks to the
// v3 API (config/*); the two domains expose the same options at different routes.
const SERIES_PATH = '/settings/downloadclient';
const MOVIES_PATH = '/config/downloadclient';

const useDownloadClientPath = () => {
  const service = useSettingsService();

  return service === 'movies' ? MOVIES_PATH : SERIES_PATH;
};

export const useDownloadClientSettings = () => {
  return useSettings<DownloadClientSettingsModel>(useDownloadClientPath());
};

export const useManageDownloadClientSettings = () => {
  return useManageSettings<DownloadClientSettingsModel>(
    useDownloadClientPath()
  );
};
