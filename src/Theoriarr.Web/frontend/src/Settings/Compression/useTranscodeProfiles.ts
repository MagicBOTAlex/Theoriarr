import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface TranscodeProfile {
  id: number;
  name: string;
  codec: string | null;
  mode: string;
  qualityValue: number;
  preset: string | null;
  targetSizeMB: number | null;
  targetPercent: number | null;
  maxHeight: number | null;
  tag: string | null;
  preferEnglishAudio: boolean;
  deviceId: string | null;
  container: string | null;
  isDefault: boolean;
}

const PATH = '/media-compression/profiles';

export const useTranscodeProfiles = () => {
  const { data, ...result } = useApiQuery<TranscodeProfile[]>({
    path: PATH,
  });

  return {
    ...result,
    profiles: data ?? [],
  };
};

export const useCreateTranscodeProfile = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    TranscodeProfile,
    TranscodeProfile
  >({
    path: PATH,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => queryClient.invalidateQueries({ queryKey: [PATH] }),
    },
  });

  return {
    createProfile: mutate,
    isCreating: isPending,
    createError: error,
  };
};

export const useUpdateTranscodeProfile = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    TranscodeProfile,
    TranscodeProfile
  >({
    path: (profile) => `${PATH}/${profile.id}`,
    method: 'PUT',
    mutationOptions: {
      onSuccess: () => queryClient.invalidateQueries({ queryKey: [PATH] }),
    },
  });

  return {
    updateProfile: mutate,
    isUpdating: isPending,
    updateError: error,
  };
};

export const useDeleteTranscodeProfile = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<void, number>({
    path: (id) => `${PATH}/${id}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => queryClient.invalidateQueries({ queryKey: [PATH] }),
    },
  });

  return {
    deleteProfile: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};
