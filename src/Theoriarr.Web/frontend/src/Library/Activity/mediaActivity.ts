import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Episode from 'Episode/Episode';
import Language from 'Language/Language';
import { Movie } from 'Movies/Movie';
import { QualityModel } from 'Quality/Quality';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import Blocklist from 'typings/Blocklist';
import History, { HistoryData } from 'typings/History';
import Queue, {
  QueueTrackedDownloadState,
  QueueTrackedDownloadStatus,
  StatusMessage,
} from 'typings/Queue';

export type MediaActivityType = 'series' | 'movie';

// Movie-side mirrors of the series resources, reduced to the fields the shared
// Activity views read. The movie API returns these under the movie API key.
export interface MovieQueueRecord {
  id: number;
  movieId?: number;
  movie?: Movie;
  title: string;
  status: string;
  trackedDownloadStatus?: QueueTrackedDownloadStatus;
  trackedDownloadState?: QueueTrackedDownloadState;
  statusMessages?: StatusMessage[];
  errorMessage?: string;
  quality?: QualityModel;
  size?: number;
  sizeleft?: number;
  timeleft?: string;
  estimatedCompletionTime?: string;
  downloadClient?: string;
  protocol?: DownloadProtocol;
  indexer?: string;
  downloadId?: string;
  downloadClientHasPostImportCategory?: boolean;
  outputPath?: string;
  suggestedOutputPath?: string;
  pathCorrectionSupported?: boolean;
  pathNotAccessible?: boolean;
  corruptFileDetected?: boolean;
}

export interface MovieHistoryRecord {
  id: number;
  movieId: number;
  movie?: Movie;
  sourceTitle: string;
  quality?: QualityModel;
  date: string;
  eventType: string;
  downloadId?: string;
  data?: Record<string, string>;
}

export interface MovieBlocklistRecord {
  id: number;
  movieId: number;
  movie?: Movie;
  sourceTitle: string;
  quality?: QualityModel;
  date: string;
  protocol?: DownloadProtocol;
  indexer?: string;
  message?: string;
}

export interface MediaQueueItem {
  key: string;
  type: MediaActivityType;
  id: number;
  searchText: string;
  title: string;
  seriesId?: number;
  episodeIds: number[];
  movieId?: number;
  status: string;
  size: number;
  sizeLeft: number;
  timeLeft?: string;
  estimatedCompletionTime?: string;
  downloadClient?: string;
  protocol?: DownloadProtocol;
  indexer?: string;
  downloadId?: string;
  isPending: boolean;
  added?: string;
  languages?: Language[];
  customFormats?: CustomFormat[];
  customFormatScore?: number;
  outputPath?: string;
  suggestedOutputPath?: string;
  pathCorrectionSupported?: boolean;
  pathNotAccessible?: boolean;
  corruptFileDetected?: boolean;
  isFullSeason?: boolean;
  seasonNumbers?: number[];
  episodes?: Episode[];
  movieTitle?: string;
  movieLink?: string;
  series?: Queue;
  movie?: MovieQueueRecord;
}

export interface MediaHistoryItem {
  key: string;
  type: MediaActivityType;
  id: number;
  searchText: string;
  date: string;
  sourceTitle: string;
  seriesId?: number;
  episodeId?: number;
  movieId?: number;
  movieTitle?: string;
  movieLink?: string;
  eventType?: string;
  historyEventType?: History['eventType'];
  historyData?: History['data'];
  movieEventType?: string;
  qualityName?: string;
  quality?: QualityModel;
  languages?: Language[];
  customFormats?: CustomFormat[];
  customFormatScore?: number;
  qualityCutoffNotMet?: boolean;
  releaseGroup?: string;
  downloadClient?: string;
  indexer?: string;
  data?: HistoryData;
  downloadId?: string;
  series?: History;
  movie?: MovieHistoryRecord;
}

export interface MediaBlocklistItem {
  key: string;
  type: MediaActivityType;
  id: number;
  searchText: string;
  date?: string;
  sourceTitle: string;
  seriesId?: number;
  movieId?: number;
  movieTitle?: string;
  movieLink?: string;
  indexer?: string;
  message?: string;
  quality?: QualityModel;
  languages?: Language[];
  customFormats?: CustomFormat[];
  protocol?: DownloadProtocol;
  source?: string;
  series?: Blocklist;
  movie?: MovieBlocklistRecord;
}

function isPendingQueueStatus(status: string) {
  return status === 'delay' || status === 'downloadClientUnavailable';
}

export function queueToMediaQueueItem(queue: Queue): MediaQueueItem {
  return {
    key: `series-${queue.id}`,
    type: 'series',
    id: queue.id,
    searchText: (queue.title ?? '').toLowerCase(),
    title: queue.title,
    seriesId: queue.seriesId,
    episodeIds: queue.episodeIds ?? [],
    status: queue.status,
    size: queue.size ?? 0,
    sizeLeft: queue.sizeLeft ?? 0,
    timeLeft: queue.timeLeft,
    estimatedCompletionTime: queue.estimatedCompletionTime,
    downloadClient: queue.downloadClient,
    protocol: queue.protocol,
    indexer: undefined,
    downloadId: queue.downloadId,
    isPending: isPendingQueueStatus(queue.status),
    added: queue.added,
    languages: queue.languages,
    customFormats: queue.customFormats,
    customFormatScore: queue.customFormatScore,
    outputPath: queue.outputPath,
    suggestedOutputPath: queue.suggestedOutputPath,
    pathCorrectionSupported: queue.pathCorrectionSupported,
    pathNotAccessible: queue.pathNotAccessible,
    corruptFileDetected: queue.corruptFileDetected,
    isFullSeason: queue.isFullSeason,
    seasonNumbers: queue.seasonNumbers,
    episodes: queue.episodes,
    movieTitle: undefined,
    movieLink: undefined,
    series: queue,
  };
}

export function movieQueueToMediaQueueItem(
  queue: MovieQueueRecord
): MediaQueueItem {
  const movieId = queue.movieId ?? queue.movie?.id;
  let movieLink: string | undefined = undefined;

  if (movieId != null) {
    movieLink = `/movie/${movieId}`;
  }

  return {
    key: `movie-${queue.id}`,
    type: 'movie',
    id: queue.id,
    searchText: `${queue.title ?? ''} ${
      queue.movie?.title ?? ''
    }`.toLowerCase(),
    title: queue.title,
    movieId: queue.movieId,
    episodeIds: [],
    status: queue.status,
    size: queue.size ?? 0,
    sizeLeft: queue.sizeleft ?? 0,
    timeLeft: queue.timeleft,
    estimatedCompletionTime: queue.estimatedCompletionTime,
    downloadClient: queue.downloadClient,
    protocol: queue.protocol,
    indexer: queue.indexer,
    downloadId: queue.downloadId,
    isPending: isPendingQueueStatus(queue.status),
    added: queue.movie?.added,
    outputPath: queue.outputPath ?? queue.movie?.path,
    suggestedOutputPath: queue.suggestedOutputPath,
    pathCorrectionSupported: queue.pathCorrectionSupported,
    pathNotAccessible: queue.pathNotAccessible,
    corruptFileDetected: queue.corruptFileDetected,
    movieTitle: queue.movie?.title,
    movieLink,
    movie: queue,
  };
}

function readHistoryData(data: History['data'] | undefined) {
  const record = (data ?? {}) as Record<string, unknown>;

  let downloadClient: string | undefined = undefined;

  if (typeof record.downloadClientName === 'string') {
    downloadClient = record.downloadClientName;
  } else if (typeof record.downloadClient === 'string') {
    downloadClient = record.downloadClient;
  }

  return {
    releaseGroup:
      typeof record.releaseGroup === 'string' ? record.releaseGroup : undefined,
    indexer: typeof record.indexer === 'string' ? record.indexer : undefined,
    downloadClient,
  };
}

export function historyToMediaHistoryItem(history: History): MediaHistoryItem {
  const { releaseGroup, indexer, downloadClient } = readHistoryData(
    history.data
  );

  return {
    key: `series-${history.id}`,
    type: 'series',
    id: history.id,
    searchText: (history.sourceTitle ?? '').toLowerCase(),
    date: history.date,
    sourceTitle: history.sourceTitle,
    seriesId: history.seriesId,
    episodeId: history.episodeId,
    eventType: history.eventType,
    historyEventType: history.eventType,
    historyData: history.data,
    data: history.data,
    qualityName: history.quality?.quality?.name,
    quality: history.quality,
    languages: history.languages,
    customFormats: history.customFormats,
    customFormatScore: history.customFormatScore,
    qualityCutoffNotMet: history.qualityCutoffNotMet,
    releaseGroup,
    indexer,
    downloadClient,
    downloadId: history.downloadId,
    series: history,
  };
}

export function movieHistoryToMediaHistoryItem(
  history: MovieHistoryRecord
): MediaHistoryItem {
  return {
    key: `movie-${history.id}`,
    type: 'movie',
    id: history.id,
    searchText: `${history.sourceTitle ?? ''} ${
      history.movie?.title ?? ''
    }`.toLowerCase(),
    date: history.date,
    sourceTitle: history.sourceTitle,
    movieId: history.movieId,
    movieTitle: history.movie?.title,
    movieLink: `/movie/${history.movieId}`,
    eventType: history.eventType,
    movieEventType: history.eventType,
    qualityName: history.quality?.quality?.name,
    quality: history.quality,
    downloadId: history.downloadId,
    movie: history,
  };
}

export function blocklistToMediaBlocklistItem(
  blocklist: Blocklist
): MediaBlocklistItem {
  return {
    key: `series-${blocklist.id}`,
    type: 'series',
    id: blocklist.id,
    searchText: (blocklist.sourceTitle ?? '').toLowerCase(),
    date: blocklist.date,
    sourceTitle: blocklist.sourceTitle,
    seriesId: blocklist.seriesId,
    indexer: blocklist.indexer,
    message: blocklist.message,
    quality: blocklist.quality,
    languages: blocklist.languages,
    customFormats: blocklist.customFormats,
    protocol: blocklist.protocol,
    source: blocklist.source,
    series: blocklist,
  };
}

export function movieBlocklistToMediaBlocklistItem(
  blocklist: MovieBlocklistRecord
): MediaBlocklistItem {
  return {
    key: `movie-${blocklist.id}`,
    type: 'movie',
    id: blocklist.id,
    searchText: `${blocklist.sourceTitle ?? ''} ${
      blocklist.movie?.title ?? ''
    }`.toLowerCase(),
    date: blocklist.date,
    sourceTitle: blocklist.sourceTitle,
    movieId: blocklist.movieId,
    movieTitle: blocklist.movie?.title,
    movieLink: `/movie/${blocklist.movieId}`,
    indexer: blocklist.indexer,
    message: blocklist.message,
    quality: blocklist.quality,
    protocol: blocklist.protocol,
    movie: blocklist,
  };
}
