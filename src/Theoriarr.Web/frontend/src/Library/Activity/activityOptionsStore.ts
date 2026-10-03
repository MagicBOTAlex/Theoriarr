import React from 'react';
import Icon from 'Components/Icon';
import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

export type ActivityQueueOptions = PageableOptions;
export type ActivityHistoryOptions = PageableOptions;
export type ActivityBlocklistOptions = PageableOptions;

const {
  useOptions: useQueueOptions,
  setOptions: setQueueOptions,
  useOption: useQueueOption,
  setOption: setQueueOption,
  setSort: setQueueSort,
} = createOptionsStore<ActivityQueueOptions>('activity_queue_options', () => {
  return {
    pageSize: 20,
    selectedFilterKey: 'all',
    sortKey: 'timeLeft',
    sortDirection: 'ascending',
    columns: [
      {
        name: 'type',
        label: () => translate('Type'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'status',
        label: '',
        columnLabel: () => translate('Status'),
        isSortable: true,
        isVisible: true,
        isModifiable: 'onlyPosition',
      },
      {
        name: 'media',
        label: () => translate('Media'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'episode',
        label: () => translate('EpisodeMaybePlural'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'episodeTitle',
        label: () => translate('EpisodeTitleMaybePlural'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'airDate',
        label: () => translate('EpisodeAirDate'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'title',
        label: () => translate('ReleaseTitle'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'languages',
        label: () => translate('Languages'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'quality',
        label: () => translate('Quality'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'customFormats',
        label: () => translate('Formats'),
        isSortable: false,
        isVisible: false,
      },
      {
        name: 'customFormatScore',
        columnLabel: () => translate('CustomFormatScore'),
        label: React.createElement(Icon, {
          name: icons.SCORE,
          title: () => translate('CustomFormatScore'),
        }),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'protocol',
        label: () => translate('Protocol'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'indexer',
        label: () => translate('Indexer'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'downloadClient',
        label: () => translate('DownloadClient'),
        isSortable: true,
        isVisible: false,
      },
      {
        name: 'size',
        label: () => translate('Size'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'outputPath',
        label: () => translate('OutputPath'),
        isSortable: false,
        isVisible: false,
      },
      {
        name: 'timeLeft',
        label: () => translate('TimeLeft'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'added',
        label: () => translate('Added'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'progress',
        label: () => translate('Progress'),
        isSortable: true,
        isVisible: true,
      },
      {
        name: 'actions',
        label: '',
        columnLabel: () => translate('Actions'),
        isVisible: true,
        isModifiable: 'onlyPosition',
      },
    ],
  };
});

const {
  useOptions: useHistoryOptions,
  setOptions: setHistoryOptions,
  useOption: useHistoryOption,
  setOption: setHistoryOption,
  setSort: setHistorySort,
} = createOptionsStore<ActivityHistoryOptions>(
  'activity_history_options',
  () => {
    return {
      pageSize: 20,
      selectedFilterKey: 'all',
      sortKey: 'date',
      sortDirection: 'descending',
      columns: [
        {
          name: 'type',
          label: () => translate('Type'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'event',
          label: () => translate('EventType'),
          isSortable: true,
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
        {
          name: 'media',
          label: () => translate('Media'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'episode',
          label: () => translate('Episode'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'episodeTitle',
          label: () => translate('EpisodeTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'quality',
          label: () => translate('Quality'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'customFormats',
          label: () => translate('Formats'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'customFormatScore',
          columnLabel: () => translate('CustomFormatScore'),
          label: React.createElement(Icon, {
            name: icons.SCORE,
            title: () => translate('CustomFormatScore'),
          }),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'date',
          label: () => translate('Date'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'downloadClient',
          label: () => translate('DownloadClient'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'indexer',
          label: () => translate('Indexer'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'releaseGroup',
          label: () => translate('ReleaseGroup'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'sourceTitle',
          label: () => translate('SourceTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'details',
          label: '',
          columnLabel: () => translate('Details'),
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
      ],
    };
  }
);

const {
  useOptions: useBlocklistOptions,
  setOptions: setBlocklistOptions,
  useOption: useBlocklistOption,
  setOption: setBlocklistOption,
  setSort: setBlocklistSort,
} = createOptionsStore<ActivityBlocklistOptions>(
  'activity_blocklist_options',
  () => {
    return {
      pageSize: 20,
      selectedFilterKey: 'all',
      sortKey: 'date',
      sortDirection: 'descending',
      columns: [
        {
          name: 'type',
          label: () => translate('Type'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'media',
          label: () => translate('Media'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'sourceTitle',
          label: () => translate('SourceTitle'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'quality',
          label: () => translate('Quality'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'customFormats',
          label: () => translate('Formats'),
          isSortable: false,
          isVisible: true,
        },
        {
          name: 'date',
          label: () => translate('Date'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'protocol',
          label: () => translate('Protocol'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'indexer',
          label: () => translate('Indexer'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'message',
          label: () => translate('Message'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'actions',
          label: '',
          columnLabel: () => translate('Actions'),
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
      ],
    };
  }
);

export {
  setBlocklistOption,
  setBlocklistOptions,
  setBlocklistSort,
  setHistoryOption,
  setHistoryOptions,
  setHistorySort,
  setQueueOption,
  setQueueOptions,
  setQueueSort,
  useBlocklistOption,
  useBlocklistOptions,
  useHistoryOption,
  useHistoryOptions,
  useQueueOption,
  useQueueOptions,
};
