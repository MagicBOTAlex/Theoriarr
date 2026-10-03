import { useManageSettings, useSettings } from 'Settings/useSettings';

export interface MetadataSourceSettingsModel {
  providarrBaseUrl: string;
  resolvedProvidarrBaseUrl: string;
  warning: string;
}

const PATH = '/settings/metadatasource';

export const useMetadataSource = () => {
  return useSettings<MetadataSourceSettingsModel>(PATH);
};

export const useManageMetadataSource = () => {
  return useManageSettings<MetadataSourceSettingsModel>(PATH);
};
