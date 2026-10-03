import moment from 'moment-timezone';
import { Filter, FilterBuilderProp } from 'Filters/Filter';
import { filterBuilderTypes, filterBuilderValueTypes } from 'Helpers/Props';
import { DateFilterValue, FilterType } from 'Helpers/Props/filterTypes';
import getFilterTypePredicate from 'Helpers/Props/getFilterTypePredicate';
import { SortDirection } from 'Helpers/Props/sortDirections';
import translate from 'Utilities/String/translate';
import {
  MediaBlocklistItem,
  MediaHistoryItem,
  MediaQueueItem,
} from './mediaActivity';
import {
  getQueueProgress,
  getQueueStatusBadge,
  isQueueItemAttention,
} from './mediaActivityStatus';

export interface ActivityMediaTitle {
  seriesTitle?: string;
  movieTitle?: string;
}

export function getMediaTitle(item: ActivityMediaTitle): string {
  return (item.seriesTitle ?? item.movieTitle ?? '').toLowerCase();
}

function getEventType(item: MediaHistoryItem): string {
  return item.eventType ?? item.historyEventType ?? item.movieEventType ?? '';
}

function durationToSeconds(value?: string): number | undefined {
  if (!value) {
    return undefined;
  }

  const parts = value.split(':').map(Number);

  if (parts.some((part) => Number.isNaN(part))) {
    return undefined;
  }

  return parts.reduce((acc, part) => acc * 60 + part, 0);
}

function dateValue(value?: string): number {
  return value ? moment(value).unix() : 0;
}

function stringDateFilter<T>(
  getDate: (item: T) => string | undefined
): Predicate<T> {
  return (item, filterValue: string | DateFilterValue, type) => {
    const value = getDate(item);

    if (!value) {
      return false;
    }

    if (typeof filterValue === 'object' && filterValue !== null) {
      const { time, value: amount } = filterValue as DateFilterValue;

      if (amount == null) {
        return false;
      }

      const now = moment();
      const unit = time as moment.unitOfTime.DurationConstructor;

      switch (type) {
        case 'inLast':
          return moment(value).isAfter(now.clone().subtract(amount, unit));
        case 'notInLast':
          return moment(value).isBefore(now.clone().subtract(amount, unit));
        case 'inNext':
          return moment(value).isBefore(now.clone().add(amount, unit));
        case 'notInNext':
          return moment(value).isAfter(now.clone().add(amount, unit));
        default:
          return false;
      }
    }

    return getFilterTypePredicate(type)(value, filterValue);
  };
}

const TYPE_FILTER_OPTIONS = () => [
  { id: 'series', name: translate('Show') },
  { id: 'movie', name: translate('Movie') },
];

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Predicate<T> = (item: T, value: any, type: FilterType) => boolean;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type SortPredicate<T> = (item: T, direction: SortDirection) => any;

function typePredicate<T extends { type: string }>(): Predicate<T> {
  return (item, value: string, type) =>
    getFilterTypePredicate(type)(item.type, value);
}

function stringPredicate<T>(getValue: (item: T) => string): Predicate<T> {
  return (item, value: string, type) =>
    getFilterTypePredicate(type)(getValue(item).toLowerCase(), value);
}

function qualityPredicate<T>(
  getQuality: (item: T) => { quality?: { name: string } } | undefined
): Predicate<T> {
  return (item, value: string, type) =>
    getFilterTypePredicate(type)(getQuality(item)?.quality?.name ?? '', value);
}

/* -------------------------------------------------------------------------- */
/*                                  Queue                                     */
/* -------------------------------------------------------------------------- */

// The queue status sort ranks by the badge kind returned by
// getQueueStatusBadge (see mediaActivityStatus.ts), so the keys must be the
// BadgeKind values (danger/warning/info/primary/purple/success/default) rather
// than raw queue status strings. Keying it by status strings meant every item
// fell through to the default rank, so sorting never changed anything.
const statusRank: Record<string, number> = {
  danger: 0,
  warning: 1,
  info: 2,
  primary: 3,
  purple: 4,
  success: 5,
  default: 6,
};

export const QUEUE_FILTERS: Filter[] = [
  { key: 'all', label: () => translate('All'), filters: [] },
  {
    key: 'shows',
    label: () => translate('Shows'),
    filters: [{ key: 'type', value: 'series', type: 'equal' }],
  },
  {
    key: 'movies',
    label: () => translate('Movies'),
    filters: [{ key: 'type', value: 'movie', type: 'equal' }],
  },
  {
    key: 'downloading',
    label: () => translate('Downloading'),
    filters: [{ key: 'status', value: 'downloading', type: 'equal' }],
  },
  {
    key: 'completed',
    label: () => translate('Completed'),
    filters: [{ key: 'status', value: 'completed', type: 'equal' }],
  },
  {
    key: 'failed',
    label: () => translate('Failed'),
    filters: [{ key: 'attention', value: [true], type: 'equal' }],
  },
];

export const QUEUE_FILTER_PREDICATES: Record<
  string,
  Predicate<MediaQueueItem>
> = {
  type: typePredicate(),
  mediaTitle: stringPredicate(getMediaTitle),
  title: stringPredicate((item) => item.title),
  status: (item, value, type) =>
    getFilterTypePredicate(type)(item.status, value),
  attention: (item, value, type) =>
    getFilterTypePredicate(type)(isQueueItemAttention(item), value),
  quality: qualityPredicate(
    (item) => item.series?.quality ?? item.movie?.quality
  ),
  protocol: (item, value, type) =>
    getFilterTypePredicate(type)(item.protocol ?? '', value),
  indexer: (item, value, type) =>
    getFilterTypePredicate(type)(item.indexer ?? '', value),
  downloadClient: (item, value, type) =>
    getFilterTypePredicate(type)(item.downloadClient ?? '', value),
  size: (item, value, type) =>
    getFilterTypePredicate(type)(item.size ?? 0, value),
  progress: (item, value, type) =>
    getFilterTypePredicate(type)(getQueueProgress(item), value),
  customFormatScore: (item, value, type) =>
    getFilterTypePredicate(type)(item.customFormatScore ?? 0, value),
  added: stringDateFilter((item: MediaQueueItem) => item.added),
};

export const QUEUE_FILTER_BUILDER: FilterBuilderProp<MediaQueueItem>[] = [
  {
    name: 'type',
    label: () => translate('Type'),
    type: filterBuilderTypes.EXACT,
    optionsSelector: TYPE_FILTER_OPTIONS,
  },
  {
    name: 'status',
    label: () => translate('Status'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUEUE_STATUS,
  },
  {
    name: 'mediaTitle',
    label: () => translate('SeriesOrMovie'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'title',
    label: () => translate('ReleaseTitle'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUALITY,
  },
  {
    name: 'protocol',
    label: () => translate('Protocol'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.PROTOCOL,
  },
  {
    name: 'indexer',
    label: () => translate('Indexer'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'downloadClient',
    label: () => translate('DownloadClient'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    type: filterBuilderTypes.NUMBER,
    valueType: filterBuilderValueTypes.BYTES,
  },
  {
    name: 'progress',
    label: () => translate('Progress'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'customFormatScore',
    label: () => translate('CustomFormatScore'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'added',
    label: () => translate('Added'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
  {
    name: 'attention',
    label: () => translate('Attention'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL,
  },
];

export const QUEUE_SORT_PREDICATES: Record<
  string,
  SortPredicate<MediaQueueItem>
> = {
  type: (item) => item.type,
  media: (item) => getMediaTitle(item),
  status: (item) => {
    const badge = getQueueStatusBadge(item);
    const rank = statusRank[badge.kind] ?? 9;

    // Tie-break by the visible status label. Several distinct statuses share a
    // badge kind ("Manual Import", "Paused", "Pending" and "Warning" are all
    // `warning`), so ranking by kind alone let the title secondary sort
    // interleave them — e.g. a "Warning" row landing between "Manual Import"
    // rows. Including the label keeps every distinct status contiguous.
    return `${String(rank).padStart(2, '0')}:${badge.label.toLowerCase()}`;
  },
  title: (item) => item.title.toLowerCase(),
  episode: (item) => item.episodeIds[0] ?? 0,
  episodeTitle: (item) => item.episodes?.[0]?.title?.toLowerCase() ?? '',
  airDate: (item) => dateValue(item.episodes?.[0]?.airDateUtc),
  languages: (item) => (item.languages ?? []).map((l) => l.name).join(', '),
  quality: (item) =>
    (item.series?.quality ?? item.movie?.quality)?.quality?.name ?? '',
  customFormats: (item) =>
    (item.customFormats ?? []).map((f) => f.name).join(', '),
  customFormatScore: (item) => item.customFormatScore ?? 0,
  protocol: (item) => item.protocol ?? '',
  indexer: (item) => item.indexer ?? '',
  downloadClient: (item) => item.downloadClient ?? '',
  size: (item) => item.size ?? 0,
  outputPath: (item) => item.outputPath ?? '',
  timeLeft: (item) =>
    durationToSeconds(item.timeLeft) ?? Number.MAX_SAFE_INTEGER,
  added: (item) => dateValue(item.added),
  progress: (item) => getQueueProgress(item),
};

/* -------------------------------------------------------------------------- */
/*                                 History                                    */
/* -------------------------------------------------------------------------- */

export const HISTORY_FILTERS: Filter[] = [
  { key: 'all', label: () => translate('All'), filters: [] },
  {
    key: 'shows',
    label: () => translate('Shows'),
    filters: [{ key: 'type', value: 'series', type: 'equal' }],
  },
  {
    key: 'movies',
    label: () => translate('Movies'),
    filters: [{ key: 'type', value: 'movie', type: 'equal' }],
  },
  {
    key: 'grabbed',
    label: () => translate('Grabbed'),
    filters: [{ key: 'eventType', value: 'grabbed', type: 'equal' }],
  },
  {
    key: 'imported',
    label: () => translate('Imported'),
    filters: [
      { key: 'eventType', value: 'downloadFolderImported', type: 'equal' },
    ],
  },
  {
    key: 'failed',
    label: () => translate('Failed'),
    filters: [{ key: 'eventType', value: 'downloadFailed', type: 'equal' }],
  },
  {
    key: 'deleted',
    label: () => translate('Deleted'),
    filters: [{ key: 'eventType', value: 'episodeFileDeleted', type: 'equal' }],
  },
  {
    key: 'renamed',
    label: () => translate('Renamed'),
    filters: [{ key: 'eventType', value: 'episodeFileRenamed', type: 'equal' }],
  },
];

export const HISTORY_FILTER_PREDICATES: Record<
  string,
  Predicate<MediaHistoryItem>
> = {
  type: typePredicate(),
  mediaTitle: stringPredicate(getMediaTitle),
  eventType: (item, value, type) =>
    getFilterTypePredicate(type)(getEventType(item), value),
  sourceTitle: stringPredicate((item) => item.sourceTitle),
  quality: qualityPredicate((item) => item.quality),
  indexer: (item, value, type) =>
    getFilterTypePredicate(type)(item.indexer ?? '', value),
  releaseGroup: (item, value, type) =>
    getFilterTypePredicate(type)(item.releaseGroup ?? '', value),
  downloadClient: (item, value, type) =>
    getFilterTypePredicate(type)(item.downloadClient ?? '', value),
  customFormatScore: (item, value, type) =>
    getFilterTypePredicate(type)(item.customFormatScore ?? 0, value),
  date: stringDateFilter((item: MediaHistoryItem) => item.date),
};

export const HISTORY_FILTER_BUILDER: FilterBuilderProp<MediaHistoryItem>[] = [
  {
    name: 'type',
    label: () => translate('Type'),
    type: filterBuilderTypes.EXACT,
    optionsSelector: TYPE_FILTER_OPTIONS,
  },
  {
    name: 'eventType',
    label: () => translate('EventType'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.HISTORY_EVENT_TYPE,
  },
  {
    name: 'mediaTitle',
    label: () => translate('SeriesOrMovie'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'sourceTitle',
    label: () => translate('SourceTitle'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUALITY,
  },
  {
    name: 'indexer',
    label: () => translate('Indexer'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'releaseGroup',
    label: () => translate('ReleaseGroup'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'downloadClient',
    label: () => translate('DownloadClient'),
    type: filterBuilderTypes.STRING,
  },
  {
    name: 'customFormatScore',
    label: () => translate('CustomFormatScore'),
    type: filterBuilderTypes.NUMBER,
  },
  {
    name: 'date',
    label: () => translate('Date'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE,
  },
];

export const HISTORY_SORT_PREDICATES: Record<
  string,
  SortPredicate<MediaHistoryItem>
> = {
  type: (item) => item.type,
  event: (item) => getEventType(item),
  media: (item) => getMediaTitle(item),
  episode: (item) => item.episodeId ?? 0,
  episodeTitle: () => '',
  languages: (item) => (item.languages ?? []).map((l) => l.name).join(', '),
  quality: (item) => item.quality?.quality?.name ?? '',
  customFormats: (item) =>
    (item.customFormats ?? []).map((f) => f.name).join(', '),
  customFormatScore: (item) => item.customFormatScore ?? 0,
  date: (item) => dateValue(item.date),
  downloadClient: (item) => item.downloadClient ?? '',
  indexer: (item) => item.indexer ?? '',
  releaseGroup: (item) => item.releaseGroup ?? '',
  sourceTitle: (item) => item.sourceTitle.toLowerCase(),
};

/* -------------------------------------------------------------------------- */
/*                                Blocklist                                   */
/* -------------------------------------------------------------------------- */

export const BLOCKLIST_FILTERS: Filter[] = [
  { key: 'all', label: () => translate('All'), filters: [] },
  {
    key: 'shows',
    label: () => translate('Shows'),
    filters: [{ key: 'type', value: 'series', type: 'equal' }],
  },
  {
    key: 'movies',
    label: () => translate('Movies'),
    filters: [{ key: 'type', value: 'movie', type: 'equal' }],
  },
];

export const BLOCKLIST_FILTER_PREDICATES: Record<
  string,
  Predicate<MediaBlocklistItem>
> = {
  type: typePredicate(),
  mediaTitle: stringPredicate(getMediaTitle),
  sourceTitle: stringPredicate((item) => item.sourceTitle),
  quality: qualityPredicate((item) => item.quality),
  protocol: (item, value, type) =>
    getFilterTypePredicate(type)(item.protocol ?? '', value),
  indexer: (item, value, type) =>
    getFilterTypePredicate(type)(item.indexer ?? '', value),
  message: stringPredicate((item) => item.message ?? ''),
  date: stringDateFilter((item: MediaBlocklistItem) => item.date),
};

export const BLOCKLIST_FILTER_BUILDER: FilterBuilderProp<MediaBlocklistItem>[] =
  [
    {
      name: 'type',
      label: () => translate('Type'),
      type: filterBuilderTypes.EXACT,
      optionsSelector: TYPE_FILTER_OPTIONS,
    },
    {
      name: 'mediaTitle',
      label: () => translate('SeriesOrMovie'),
      type: filterBuilderTypes.STRING,
    },
    {
      name: 'sourceTitle',
      label: () => translate('SourceTitle'),
      type: filterBuilderTypes.STRING,
    },
    {
      name: 'quality',
      label: () => translate('Quality'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY,
    },
    {
      name: 'protocol',
      label: () => translate('Protocol'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.PROTOCOL,
    },
    {
      name: 'indexer',
      label: () => translate('Indexer'),
      type: filterBuilderTypes.STRING,
    },
    {
      name: 'message',
      label: () => translate('Message'),
      type: filterBuilderTypes.STRING,
    },
    {
      name: 'date',
      label: () => translate('Date'),
      type: filterBuilderTypes.DATE,
      valueType: filterBuilderValueTypes.DATE,
    },
  ];

export const BLOCKLIST_SORT_PREDICATES: Record<
  string,
  SortPredicate<MediaBlocklistItem>
> = {
  type: (item) => item.type,
  media: (item) => getMediaTitle(item),
  sourceTitle: (item) => item.sourceTitle.toLowerCase(),
  languages: (item) => (item.languages ?? []).map((l) => l.name).join(', '),
  quality: (item) => item.quality?.quality?.name ?? '',
  customFormats: (item) =>
    (item.customFormats ?? []).map((f) => f.name).join(', '),
  date: (item) => dateValue(item.date),
  protocol: (item) => item.protocol ?? '',
  indexer: (item) => item.indexer ?? '',
  message: (item) => (item.message ?? '').toLowerCase(),
};
