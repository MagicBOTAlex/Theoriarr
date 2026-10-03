import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';

export type CompressMediaType = 'series' | 'movie';

export interface CompressOptions {
  seriesProfileId: string;
  seriesDeviceId: string;
  movieProfileId: string;
  movieDeviceId: string;
}

const { useOptions, useOption, getOption, setOption } =
  createOptionsStore<CompressOptions>('compress_media_options', () => {
    return {
      seriesProfileId: '',
      seriesDeviceId: '',
      movieProfileId: '',
      movieDeviceId: '',
    };
  });

export function getCompressProfileKey(
  mediaType: CompressMediaType
): keyof CompressOptions {
  return mediaType === 'movie' ? 'movieProfileId' : 'seriesProfileId';
}

export function getCompressDeviceKey(
  mediaType: CompressMediaType
): keyof CompressOptions {
  return mediaType === 'movie' ? 'movieDeviceId' : 'seriesDeviceId';
}

export const useCompressOptions = useOptions;
export const useCompressOption = useOption;
export const getCompressOption = getOption;
export const setCompressOption = setOption;
