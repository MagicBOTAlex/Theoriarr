import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface TranscodeJob {
  id: number;
  mediaType: string;
  episodeFileId?: number;
  movieFileId?: number;
  seriesId?: number;
  movieId?: number;
  seriesTitle?: string;
  seasonNumber?: number;
  movieTitle?: string;
  sourcePath: string;
  outputPath?: string;
  status: string;
  mode: string;
  videoCodec?: string;
  rateControl?: string;
  preset?: string;
  deviceId?: string;
  targetSize?: number;
  targetPercent?: number;
  qualityValue?: number;
  maxHeight?: number;
  tag?: string;
  preferEnglishAudio?: boolean;
  profileId?: number;
  requeuedJobId?: number;
  sourceSize?: number;
  outputSize?: number;
  progress: number;
  speed?: string;
  fps?: string;
  eta?: string;
  error?: string;
  message?: string;
  startedAt?: string;
  endedAt?: string;
  lastUpdatedAt?: string;
}

export interface TranscodeRequest {
  episodeFileIds?: number[];
  movieFileIds?: number[];
  seriesIds?: number[];
  seasonNumbers?: number[];
  movieIds?: number[];
  codec?: string;
  mode?: string;
  targetSize?: number;
  targetPercent?: number;
  quality?: number;
  maxHeight?: number;
  preset?: string;
  deviceId?: string;
  profileId?: number;
  container?: string;
  force?: boolean;
  requeueJobIds?: number[];
}

export interface TranscodeJobsResponse {
  jobs: TranscodeJob[];
  projectedSavingsBytes: number;
  requestedCount?: number;
  skippedCount?: number;
}

const JOBS_PATH = '/media-compression/jobs';
const BLOCKED_PATH = `${JOBS_PATH}/blocked`;

export const useTranscodeJobs = () => {
  const { data, ...result } = useApiQuery<TranscodeJob[]>({
    path: JOBS_PATH,
    queryOptions: {
      // Only fast-poll while work can actually change server-side (queued, encoding, transferring);
      // otherwise stop entirely so an idle page does not re-fetch the full history every 3s.
      refetchInterval: (query) => {
        const jobs = query.state.data;

        const busy = jobs?.some(
          (job) =>
            job.status === 'Queued' ||
            job.status === 'Running' ||
            job.status === 'Transferring'
        );

        return busy ? 3000 : false;
      },
    },
  });

  return {
    ...result,
    jobs: data ?? [],
  };
};

export const useCreateTranscodeJobs = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, reset } = useApiMutation<
    TranscodeJobsResponse,
    TranscodeRequest
  >({
    path: JOBS_PATH,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
      },
    },
  });

  return {
    createJobs: mutate,
    isCreating: isPending,
    createError: error,
    resetCreateError: reset,
  };
};

export const useCancelTranscodeJob = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, reset } = useApiMutation<
    void,
    { jobId: number }
  >({
    path: ({ jobId }) => `${JOBS_PATH}/${jobId}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
      },
    },
  });

  return {
    cancelJob: mutate,
    isCancelling: isPending,
    cancelError: error,
    resetCancelError: reset,
  };
};

export const useClearTranscodeHistory = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    { clearedCount: number },
    void
  >({
    path: `${JOBS_PATH}/history`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
      },
    },
  });

  return {
    clearHistory: mutate,
    isClearingHistory: isPending,
    clearHistoryError: error,
  };
};

export const useBlockedTranscodeJobs = () => {
  const { data, refetch, isFetching } = useApiQuery<TranscodeJob[]>({
    path: BLOCKED_PATH,
    queryOptions: {
      enabled: false,
    },
  });

  return {
    blockedJobs: data ?? [],
    refetchBlockedJobs: refetch,
    isFetchingBlockedJobs: isFetching,
  };
};

export const useForceStopTranscodeJobs = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    TranscodeJob[],
    { jobIds?: number[] }
  >({
    path: `${JOBS_PATH}/force-stop`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
        queryClient.invalidateQueries({ queryKey: [BLOCKED_PATH] });
      },
    },
  });

  return {
    forceStopJobs: mutate,
    isForceStopping: isPending,
    forceStopError: error,
  };
};

export interface TranscodeBulkResolveFailure {
  jobId: number;
  error: string;
}

export interface TranscodeBulkResolveResponse {
  resolved: TranscodeJob[];
  failed: TranscodeBulkResolveFailure[];
}

export const useBulkResolveTranscodeJobs = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    TranscodeBulkResolveResponse,
    { action: string; jobIds: number[] }
  >({
    path: `${JOBS_PATH}/bulk-resolve`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
      },
    },
  });

  return {
    bulkResolveJobs: mutate,
    isBulkResolving: isPending,
    bulkResolveError: error,
  };
};

export const useResolveTranscodeJob = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, reset } = useApiMutation<
    TranscodeJob,
    { jobId: number; action: string }
  >({
    path: ({ jobId }) => `${JOBS_PATH}/${jobId}/resolve`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: [JOBS_PATH] });
      },
    },
  });

  return {
    resolveJob: mutate,
    isResolving: isPending,
    resolveError: error,
    resetResolveError: reset,
  };
};
