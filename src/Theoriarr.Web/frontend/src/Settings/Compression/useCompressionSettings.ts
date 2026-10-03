import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface CompressionSettings {
  mediaCompressionEnabled: boolean;
  transcodeTempFolder: string | null;
  maxConcurrentJobs: number;
  defaultVideoCodec: string | null;
  defaultRateControlMode: string;
  defaultTargetEpisodeSizeMB: number;
  defaultTargetMovieSizeMB: number;
  defaultQualityValue: number;
  defaultPreset: string | null;
  defaultReducePercent: number;
  transcodeReviewDefault: string;
  preferHardware: boolean;
  ffmpegPath: string | null;
  transcodeNice: number;
  transcodeEasiestJobsFirst: boolean;
}

const PATH = '/settings/compression';

export const useCompressionSettings = () => {
  const { data, ...result } = useApiQuery<CompressionSettings>({ path: PATH });

  return {
    ...result,
    settings: data,
  };
};

export const useUpdateCompressionSettings = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    CompressionSettings,
    CompressionSettings
  >({
    path: PATH,
    method: 'PUT',
    mutationOptions: {
      // Write the server's response straight into the cache. Refetching instead
      // leaves a window where a subsequent autosave would merge against stale data.
      onSuccess: (updatedSettings) =>
        queryClient.setQueryData([PATH], updatedSettings),
    },
  });

  return {
    updateSettings: mutate,
    isUpdating: isPending,
    updateError: error,
  };
};
