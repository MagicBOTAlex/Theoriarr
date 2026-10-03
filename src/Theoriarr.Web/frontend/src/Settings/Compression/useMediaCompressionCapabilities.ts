import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface TranscodeDeviceCapability {
  id: string;
  kind: string;
  name: string;
  supported: boolean;
  encoders: string[];
  decoders: string[];
  pixelFormats: string[];
  rateControls: string[];
  presets: string[];
  profiles: string[];
  filters: string[];
  tonemap: string | null;
  maxSessions: number;
  vramMB: number;
  driver: string | null;
}

export interface TranscodeCodecSetting {
  codec: string;
  encoder: string | null;
  supported: boolean;
  enabled: boolean;
}

export interface TranscodeDevice {
  id: number;
  deviceId: string;
  kind: string;
  name: string;
  label: string;
  supported: boolean;
  enabled: boolean;
  maxParallel: number;
  priority: number;
  weight: number;
  unavailable: boolean;
  codecs: TranscodeCodecSetting[];
  capabilities: TranscodeDeviceCapability;
}

export interface MediaCompressionCapabilities {
  fingerprint: string;
  lastProbed: string;
  ffmpegPath: string;
  ffmpegVersion: string;
  devices: TranscodeDevice[];
}

const PATH = '/media-compression/capabilities';

export function getDeviceLabel(device: TranscodeDevice) {
  return device.label || device.name;
}

export const useMediaCompressionCapabilities = () => {
  const { data, ...result } = useApiQuery<MediaCompressionCapabilities>({
    path: PATH,
  });

  return {
    ...result,
    capabilities: data ?? ({} as MediaCompressionCapabilities),
  };
};

export const useUpdateTranscodeDevices = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    TranscodeDevice[],
    TranscodeDevice[]
  >({
    path: '/media-compression/devices',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (devices) => {
        queryClient.setQueryData<MediaCompressionCapabilities>([PATH], (old) =>
          old ? { ...old, devices } : old
        );
      },
    },
  });

  return {
    updateDevices: mutate,
    isUpdating: isPending,
    updateError: error,
  };
};

export const useReprobeMediaCompression = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    MediaCompressionCapabilities,
    { force?: boolean }
  >({
    path: `${PATH}/reprobe`,
    method: 'POST',
    mutationOptions: {
      onSuccess: (data) => {
        queryClient.setQueryData<MediaCompressionCapabilities>([PATH], data);
      },
    },
  });

  return {
    reprobe: mutate,
    isReprobing: isPending,
    reprobeError: error,
  };
};
