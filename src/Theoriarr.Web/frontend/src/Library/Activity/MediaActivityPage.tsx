import classNames from 'classnames';
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useLocation } from 'react-router-dom';
import RemoveQueueItemModal from 'Activity/Queue/RemoveQueueItemModal';
import Icon, { IconName } from 'Components/Icon';
import Link from 'Components/Link/Link';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import { icons, kinds } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import useSeries from 'Series/useSeries';
import { CheckInputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import translate from 'Utilities/String/translate';
import ActivityFilterMenu from './ActivityFilterMenu';
import { ActivityTab } from './ActivityFilterModal';
import {
  BLOCKLIST_FILTER_PREDICATES,
  BLOCKLIST_FILTERS,
  BLOCKLIST_SORT_PREDICATES,
  HISTORY_FILTER_PREDICATES,
  HISTORY_FILTERS,
  HISTORY_SORT_PREDICATES,
  QUEUE_FILTER_PREDICATES,
  QUEUE_FILTERS,
  QUEUE_SORT_PREDICATES,
} from './activityFilters';
import {
  setBlocklistOption,
  setBlocklistSort,
  setHistoryOption,
  setHistorySort,
  setQueueOption,
  setQueueSort,
  useBlocklistOptions,
  useHistoryOptions,
  useQueueOptions,
} from './activityOptionsStore';
import {
  MediaBlocklistItem,
  MediaHistoryItem,
  MediaQueueItem,
} from './mediaActivity';
import {
  useBulkGrabMediaQueueItems,
  useBulkRemoveMediaBlocklistItems,
  useBulkRemoveMediaQueueItems,
  useClearMediaBlocklist,
} from './mediaActivityActions';
import { ACTIVITY_TONE_CLASSES } from './MediaActivityBadges';
import {
  isQueueItemActive,
  isQueueItemAttention,
  isQueueItemCompleted,
} from './mediaActivityStatus';
import MediaBlocklistRow from './MediaBlocklistRow';
import MediaHistoryRow from './MediaHistoryRow';
import MediaQueueRow from './MediaQueueRow';
import {
  useMediaBlocklist,
  useMediaHistory,
  useMediaQueue,
} from './useMediaActivity';

const TABS: { key: ActivityTab; label: () => string; to: string }[] = [
  { key: 'queue', label: () => translate('Queue'), to: '/activity/queue' },
  {
    key: 'history',
    label: () => translate('History'),
    to: '/activity/history',
  },
  {
    key: 'blocklist',
    label: () => translate('Blocklist'),
    to: '/activity/blocklist',
  },
];

const TAB_ICONS: Record<ActivityTab, IconName> = {
  queue: icons.QUEUED,
  history: icons.HISTORY,
  blocklist: icons.BLOCKLIST,
};

const TABLE_CARD_CLASS =
  'flex flex-1 flex-col overflow-hidden rounded-[10px] border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] shadow-[0_1px_3px_var(--cardShadowColor)]';

const ACTIVITY_TABLE_CLASS = classNames(
  'w-full max-w-full border-collapse',
  '[&_thead]:bg-[var(--pageFooterBackground)]',
  '[&_th]:px-[12px] [&_th]:py-[11px] [&_th]:align-middle [&_th]:text-[11px] [&_th]:font-semibold [&_th]:uppercase [&_th]:tracking-[0.06em] [&_th]:text-[var(--disabledColor)]',
  '[&_td]:border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] [&_td]:px-[12px] [&_td]:py-[12px] [&_td]:align-middle [&_td]:text-[13px]',
  '[&_tbody_tr:first-child_td]:border-t-0'
);

const PAGER_BUTTON_CLASS =
  'inline-flex items-center gap-[4px] rounded-[7px] border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] px-[10px] py-[5px] text-[12px] font-medium text-[var(--textColor)] transition-colors enabled:hover:border-[var(--themeBlue)] enabled:hover:text-[var(--themeBlue)] disabled:cursor-not-allowed disabled:opacity-40';

const FILTER_CONFIG = {
  queue: {
    filters: QUEUE_FILTERS,
    predicates: QUEUE_FILTER_PREDICATES,
    sortPredicates: QUEUE_SORT_PREDICATES,
  },
  history: {
    filters: HISTORY_FILTERS,
    predicates: HISTORY_FILTER_PREDICATES,
    sortPredicates: HISTORY_SORT_PREDICATES,
  },
  blocklist: {
    filters: BLOCKLIST_FILTERS,
    predicates: BLOCKLIST_FILTER_PREDICATES,
    sortPredicates: BLOCKLIST_SORT_PREDICATES,
  },
};

function getTabFromPath(pathname: string): ActivityTab {
  if (pathname.startsWith('/activity/history')) {
    return 'history';
  }

  if (pathname.startsWith('/activity/blocklist')) {
    return 'blocklist';
  }

  return 'queue';
}

function getHistoryEventType(item: MediaHistoryItem) {
  return item.eventType ?? item.historyEventType ?? item.movieEventType;
}

interface StatCardProps {
  icon: IconName;
  tone: keyof typeof ACTIVITY_TONE_CLASSES;
  value: number;
  label: string;
}

function StatCard({ icon, tone, value, label }: StatCardProps) {
  return (
    <div className="flex min-w-0 flex-1 items-center gap-[12px] rounded-[10px] border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] px-[16px] py-[12px] shadow-[0_1px_3px_var(--cardShadowColor)]">
      <span
        className={classNames(
          'flex h-[38px] w-[38px] shrink-0 items-center justify-center rounded-[9px]',
          ACTIVITY_TONE_CLASSES[tone]
        )}
      >
        <Icon name={icon} size={16} aria-hidden={true} />
      </span>

      <div className="min-w-0">
        <div className="text-[20px] font-semibold leading-none tabular-nums text-[var(--textColor)]">
          {value}
        </div>
        <div className="mt-[5px] truncate text-[12px] text-[var(--disabledColor)]">
          {label}
        </div>
      </div>
    </div>
  );
}

function MediaActivityPage() {
  const { pathname } = useLocation();
  const tab = getTabFromPath(pathname);

  const queue = useMediaQueue();
  const history = useMediaHistory();
  const blocklist = useMediaBlocklist();

  const queueOptions = useQueueOptions();
  const historyOptions = useHistoryOptions();
  const blocklistOptions = useBlocklistOptions();

  const queueCustomFilters = useCustomFiltersList('activity-queue');
  const historyCustomFilters = useCustomFiltersList('activity-history');
  const blocklistCustomFilters = useCustomFiltersList('activity-blocklist');

  const { seriesMap } = useSeries();

  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
  const lastSelectedKey = useRef<string | null>(null);
  const [isConfirmRemoveModalOpen, setIsConfirmRemoveModalOpen] =
    useState(false);
  const [isInteractiveImportOpen, setIsInteractiveImportOpen] = useState(false);
  const [isConfirmClearModalOpen, setIsConfirmClearModalOpen] = useState(false);

  const { removeQueueItems, isRemoving } = useBulkRemoveMediaQueueItems();
  const { grabQueueItems, isGrabbing } = useBulkGrabMediaQueueItems();
  const { removeBlocklistItems, isRemoving: isRemovingBlocklist } =
    useBulkRemoveMediaBlocklistItems();
  const { clearBlocklist, isClearing } = useClearMediaBlocklist();

  let options = queueOptions;

  if (tab === 'history') {
    options = historyOptions;
  } else if (tab === 'blocklist') {
    options = blocklistOptions;
  }

  let customFilters = queueCustomFilters;

  if (tab === 'history') {
    customFilters = historyCustomFilters;
  } else if (tab === 'blocklist') {
    customFilters = blocklistCustomFilters;
  }

  const config = FILTER_CONFIG[tab];

  let items: (MediaQueueItem | MediaHistoryItem | MediaBlocklistItem)[] =
    queue.items;
  let isLoading = queue.isLoading;
  let error = queue.error;

  if (tab === 'history') {
    items = history.items;
    isLoading = history.isLoading;
    error = history.error;
  } else if (tab === 'blocklist') {
    items = blocklist.items;
    isLoading = blocklist.isLoading;
    error = blocklist.error;
  }

  const enrichedItems = useMemo(() => {
    return items.map((item) => {
      if (item.seriesId != null) {
        const series = seriesMap.get(item.seriesId);

        if (series) {
          return { ...item, seriesTitle: series.title };
        }
      }

      return item;
    });
  }, [items, seriesMap]);

  const filteredData = useMemo(() => {
    return clientSideFilterAndSort(enrichedItems, {
      selectedFilterKey: options.selectedFilterKey,
      filters: [...config.filters],
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      filterPredicates: config.predicates as any,
      customFilters,
      sortKey: options.sortKey,
      sortDirection: options.sortDirection as SortDirection,
      secondarySortKey: 'title',
      secondarySortDirection: 'ascending',
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      sortPredicates: config.sortPredicates as any,
    }).data;
  }, [
    config,
    customFilters,
    enrichedItems,
    options.selectedFilterKey,
    options.sortDirection,
    options.sortKey,
  ]);

  const searchedData = useMemo(() => {
    const term = search.trim().toLowerCase();

    if (!term) {
      return filteredData;
    }

    return filteredData.filter((item) => item.searchText.includes(term));
  }, [filteredData, search]);

  useEffect(() => {
    setPage(1);
  }, [tab, options.selectedFilterKey, options.sortKey, search]);

  useEffect(() => {
    setSelectedKeys(new Set());
    lastSelectedKey.current = null;
  }, [tab]);

  const handleSearchChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setSearch(event.target.value);
    },
    []
  );

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      if (tab === 'queue') {
        setQueueOption('selectedFilterKey', selectedFilterKey);
      } else if (tab === 'history') {
        setHistoryOption('selectedFilterKey', selectedFilterKey);
      } else {
        setBlocklistOption('selectedFilterKey', selectedFilterKey);
      }
    },
    [tab]
  );

  const handleSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      const payload = { sortKey, sortDirection };

      if (tab === 'queue') {
        setQueueSort(payload);
      } else if (tab === 'history') {
        setHistorySort(payload);
      } else {
        setBlocklistSort(payload);
      }
    },
    [tab]
  );

  const handleTableOptionChange = useCallback(
    (payload: { pageSize?: number; columns?: Column[] }) => {
      const setVisible = (
        name: 'pageSize' | 'columns',
        value: number | Column[]
      ) => {
        if (name === 'pageSize') {
          if (tab === 'queue') {
            setQueueOption('pageSize', value as number);
          } else if (tab === 'history') {
            setHistoryOption('pageSize', value as number);
          } else {
            setBlocklistOption('pageSize', value as number);
          }
        } else if (tab === 'queue') {
          setQueueOption('columns', value as Column[]);
        } else if (tab === 'history') {
          setHistoryOption('columns', value as Column[]);
        } else {
          setBlocklistOption('columns', value as Column[]);
        }
      };

      if (payload.pageSize != null) {
        setVisible('pageSize', payload.pageSize);
      }

      if (payload.columns) {
        setVisible('columns', payload.columns);
      }
    },
    [tab]
  );

  const handlePreviousPagePress = useCallback(() => {
    setPage((previous) => Math.max(previous - 1, 1));
  }, []);

  const handleNextPagePress = useCallback(() => {
    setPage((previous) => previous + 1);
  }, []);

  const handleQueueRowModalOpenOrClose = useCallback(() => {
    // Kept for the queue row modals; no page-wide refetch pause needed.
  }, []);

  const stats = useMemo(() => {
    if (tab === 'queue') {
      const queueItems = items as MediaQueueItem[];
      const downloading = queueItems.filter(isQueueItemActive).length;
      const completed = queueItems.filter(isQueueItemCompleted).length;
      const attention = queueItems.filter(isQueueItemAttention).length;

      return [
        {
          icon: icons.QUEUED,
          tone: 'primary' as const,
          value: queueItems.length,
          label: translate('InQueue'),
        },
        {
          icon: icons.DOWNLOADING,
          tone: 'info' as const,
          value: downloading,
          label: translate('Downloading'),
        },
        {
          icon: icons.DOWNLOADED,
          tone: 'success' as const,
          value: completed,
          label: translate('Completed'),
        },
        {
          icon: icons.WARNING,
          tone: attention ? ('danger' as const) : ('default' as const),
          value: attention,
          label: translate('Attention'),
        },
      ];
    }

    if (tab === 'history') {
      const historyItems = items as MediaHistoryItem[];
      const grabbed = historyItems.filter(
        (item) => getHistoryEventType(item) === 'grabbed'
      ).length;
      const imported = historyItems.filter((item) => {
        const eventType = getHistoryEventType(item);

        return (
          eventType === 'downloadFolderImported' ||
          eventType === 'seriesFolderImported'
        );
      }).length;
      const failed = historyItems.filter(
        (item) => getHistoryEventType(item) === 'downloadFailed'
      ).length;

      return [
        {
          icon: icons.HISTORY,
          tone: 'primary' as const,
          value: historyItems.length,
          label: translate('Events'),
        },
        {
          icon: icons.DOWNLOADING,
          tone: 'info' as const,
          value: grabbed,
          label: translate('Grabbed'),
        },
        {
          icon: icons.DOWNLOADED,
          tone: 'success' as const,
          value: imported,
          label: translate('Imported'),
        },
        {
          icon: icons.DANGER,
          tone: failed ? ('danger' as const) : ('default' as const),
          value: failed,
          label: translate('Failed'),
        },
      ];
    }

    const blocklistItems = items as MediaBlocklistItem[];
    const shows = blocklistItems.filter(
      (item) => item.type === 'series'
    ).length;
    const movies = blocklistItems.filter(
      (item) => item.type === 'movie'
    ).length;

    return [
      {
        icon: icons.BLOCKLIST,
        tone: 'purple' as const,
        value: blocklistItems.length,
        label: translate('Blocklisted'),
      },
      {
        icon: icons.SERIES_CONTINUING,
        tone: 'info' as const,
        value: shows,
        label: translate('Shows'),
      },
      {
        icon: icons.MOVIES,
        tone: 'primary' as const,
        value: movies,
        label: translate('Movies'),
      },
    ];
  }, [items, tab]);

  const pageSize = options.pageSize;
  const totalPages = Math.max(Math.ceil(searchedData.length / pageSize), 1);
  const currentPage = Math.min(page, totalPages);
  const pageItems = searchedData.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const canSelect = tab === 'queue' || tab === 'blocklist';

  const pageItemKeys = useMemo(
    () => pageItems.map((item) => item.key),
    [pageItems]
  );

  const selectedItems = useMemo(
    () => searchedData.filter((item) => selectedKeys.has(item.key)),
    [searchedData, selectedKeys]
  );
  const selectedCount = selectedItems.length;
  const hasSelection = selectedCount > 0;
  const allSelected =
    pageItemKeys.length > 0 &&
    pageItemKeys.every((key) => selectedKeys.has(key));
  const allUnselected = pageItemKeys.every((key) => !selectedKeys.has(key));

  const queueSelected = selectedItems as MediaQueueItem[];
  const canBulkChangeCategory =
    tab === 'queue' &&
    queueSelected.every((item) =>
      item.type === 'series'
        ? !!item.series?.downloadClientHasPostImportCategory
        : !!item.movie?.downloadClientHasPostImportCategory
    );
  const canBulkIgnore =
    tab === 'queue' &&
    queueSelected.every((item) =>
      item.type === 'series'
        ? !!item.seriesId && (item.episodeIds?.length ?? 0) > 0
        : !!item.movieId
    );
  const isBulkPending =
    tab === 'queue' && queueSelected.every((item) => item.isPending);
  const isAnySelectedPending =
    tab === 'queue' && queueSelected.some((item) => item.isPending);
  const selectedDownloadIds = useMemo(
    () =>
      queueSelected
        .map((item) => item.downloadId)
        .filter((id): id is string => !!id),
    [queueSelected]
  );

  const handleSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setSelectedKeys((previous) => {
        const next = new Set(previous);

        pageItemKeys.forEach((key) => {
          if (value) {
            next.add(key);
          } else {
            next.delete(key);
          }
        });

        return next;
      });
    },
    [pageItemKeys]
  );

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps<string>) => {
      if (value === null) {
        return;
      }

      // Capture before scheduling the update: the state updater runs later, after
      // lastSelectedKey.current has already been set to this row.
      const lastKey = lastSelectedKey.current;

      setSelectedKeys((previous) => {
        const next = new Set(previous);

        if (shiftKey && lastKey && lastKey !== id) {
          const start = pageItemKeys.indexOf(lastKey);
          const end = pageItemKeys.indexOf(id);

          if (start !== -1 && end !== -1) {
            const from = Math.min(start, end);
            const to = Math.max(start, end);

            for (let index = from; index <= to; index++) {
              if (value) {
                next.add(pageItemKeys[index]);
              } else {
                next.delete(pageItemKeys[index]);
              }
            }

            return next;
          }
        }

        if (value) {
          next.add(id);
        } else {
          next.delete(id);
        }

        return next;
      });

      lastSelectedKey.current = id;
    },
    [pageItemKeys]
  );

  const clearSelection = useCallback(() => {
    setSelectedKeys(new Set());
    lastSelectedKey.current = null;
  }, []);

  const handleGrabSelectedPress = useCallback(() => {
    grabQueueItems(queueSelected);
    clearSelection();
  }, [grabQueueItems, queueSelected, clearSelection]);

  const handleRemoveSelectedPress = useCallback(() => {
    setIsConfirmRemoveModalOpen(true);
  }, []);

  const handleRemoveSelectedConfirmed = useCallback(() => {
    if (tab === 'blocklist') {
      removeBlocklistItems(selectedItems);
    } else {
      removeQueueItems(selectedItems);
    }

    setIsConfirmRemoveModalOpen(false);
    clearSelection();
  }, [
    tab,
    removeBlocklistItems,
    removeQueueItems,
    selectedItems,
    clearSelection,
  ]);

  const handleConfirmRemoveModalClose = useCallback(() => {
    setIsConfirmRemoveModalOpen(false);
  }, []);

  const handleImportSelectedPress = useCallback(() => {
    setIsInteractiveImportOpen(true);
  }, []);

  const handleInteractiveImportModalClose = useCallback(() => {
    setIsInteractiveImportOpen(false);
    clearSelection();
  }, [clearSelection]);

  const handleClearBlocklistPress = useCallback(() => {
    setIsConfirmClearModalOpen(true);
  }, []);

  const handleClearBlocklistConfirmed = useCallback(() => {
    clearBlocklist();
    setIsConfirmClearModalOpen(false);
    clearSelection();
    setPage(1);
  }, [clearBlocklist, clearSelection]);

  const handleConfirmClearModalClose = useCallback(() => {
    setIsConfirmClearModalOpen(false);
  }, []);

  const SELECTION_BAR_CLASS =
    'flex flex-wrap items-center gap-[10px] border-b border-[var(--defaultBorderColor)] bg-[color-mix(in_srgb,var(--themeBlue)_8%,transparent)] px-[14px] py-[9px]';

  const SELECTION_BUTTON_CLASS =
    'inline-flex items-center gap-[6px] rounded-[7px] border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] px-[11px] py-[5px] text-[12px] font-medium text-[var(--textColor)] transition-colors enabled:hover:border-[var(--themeBlue)] enabled:hover:text-[var(--themeBlue)] disabled:cursor-not-allowed disabled:opacity-40';

  const tabCounts: Record<ActivityTab, number> = {
    queue: queue.items.length,
    history: history.items.length,
    blocklist: blocklist.items.length,
  };

  const rangeStart = searchedData.length ? (currentPage - 1) * pageSize + 1 : 0;
  const rangeEnd = Math.min(currentPage * pageSize, searchedData.length);

  return (
    <PageContent title={translate('Activity')}>
      <header className="border-b border-[var(--defaultBorderColor)] px-[20px] pb-[16px] pt-[18px]">
        <div className="flex flex-wrap items-start justify-between gap-[16px]">
          <div className="flex items-center gap-[14px]">
            <span className="flex h-[44px] w-[44px] items-center justify-center rounded-[11px] bg-[color-mix(in_srgb,var(--themeBlue)_14%,transparent)] text-[var(--themeBlue)]">
              <Icon name={icons.ACTIVITY} size={20} />
            </span>

            <div>
              <h1 className="text-[20px] font-semibold leading-tight text-[var(--textColor)]">
                {translate('Activity')}
              </h1>
              <p className="mt-[3px] text-[13px] text-[var(--disabledColor)]">
                {translate('ActivityDescription')}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-[10px]">
            {tab === 'blocklist' && blocklist.items.length ? (
              <button
                type="button"
                className={SELECTION_BUTTON_CLASS}
                disabled={isClearing}
                onClick={handleClearBlocklistPress}
              >
                <Icon name={icons.CLEAR} size={12} aria-hidden={true} />
                {translate('Clear')}
              </button>
            ) : null}

            <ActivityFilterMenu
              type={tab}
              items={enrichedItems}
              selectedFilterKey={options.selectedFilterKey}
              filters={[...config.filters]}
              customFilters={customFilters}
              onFilterSelect={handleFilterSelect}
            />

            <div className="relative">
              <Icon
                name={icons.SEARCH}
                size={13}
                className="pointer-events-none absolute left-[11px] top-1/2 -translate-y-1/2 text-[var(--disabledColor)]"
              />
              <input
                className="w-[230px] rounded-[8px] border border-[var(--inputBorderColor)] bg-[var(--inputBackgroundColor)] py-[8px] pl-[33px] pr-[12px] text-[13px] text-[var(--textColor)] outline-none transition-colors focus:border-[var(--inputFocusBorderColor)]"
                type="search"
                placeholder={translate('Search')}
                value={search}
                onChange={handleSearchChange}
              />
            </div>
          </div>
        </div>

        <div className="mt-[16px] flex items-center">
          <div className="inline-flex items-center gap-[2px] rounded-[10px] bg-[var(--pageFooterBackground)] p-[4px]">
            {TABS.map((option) => {
              const isActive = option.key === tab;

              return (
                <Link
                  key={option.key}
                  className={classNames(
                    'flex items-center gap-[7px] rounded-[7px] px-[14px] py-[7px] text-[13px] font-medium transition-colors',
                    isActive
                      ? 'bg-[var(--cardBackgroundColor)] text-[var(--textColor)] shadow-[0_1px_2px_var(--cardShadowColor)]'
                      : 'text-[var(--disabledColor)] hover:text-[var(--textColor)]'
                  )}
                  to={option.to}
                >
                  {option.label()}

                  <span
                    className={classNames(
                      'rounded-full px-[7px] py-[1px] text-[11px] font-semibold leading-[16px] tabular-nums',
                      isActive
                        ? 'bg-[var(--pageFooterBackground)] text-[var(--textColor)]'
                        : 'bg-[var(--cardBackgroundColor)] text-[var(--disabledColor)]'
                    )}
                  >
                    {tabCounts[option.key]}
                  </span>
                </Link>
              );
            })}
          </div>
        </div>
      </header>

      <PageContentBody innerClassName="flex min-h-full flex-col gap-[16px] p-[18px]">
        <div className="flex flex-wrap gap-[12px]">
          {stats.map((stat) => (
            <StatCard
              key={stat.label}
              icon={stat.icon}
              tone={stat.tone}
              value={stat.value}
              label={stat.label}
            />
          ))}
        </div>

        <div className={TABLE_CARD_CLASS}>
          {isLoading && !items.length ? (
            <div className="flex flex-col gap-[10px] p-[16px]">
              {Array.from({ length: 6 }).map((_, index) => (
                <div
                  key={index}
                  className="h-[38px] animate-pulse rounded-[7px] bg-[var(--pageFooterBackground)]"
                />
              ))}
            </div>
          ) : null}

          {error ? (
            <div className="m-[16px] rounded-[8px] border border-[var(--dangerColor)] bg-[color-mix(in_srgb,var(--dangerColor)_10%,transparent)] px-[14px] py-[10px] text-[13px] text-[var(--dangerColor)]">
              {translate('ActivityLoadError')}: {error.message}
            </div>
          ) : null}

          {!isLoading && !error && !pageItems.length ? (
            <div className="flex flex-1 flex-col items-center justify-center gap-[12px] px-[24px] py-[64px] text-center">
              <span className="flex h-[58px] w-[58px] items-center justify-center rounded-full bg-[var(--pageFooterBackground)] text-[var(--disabledColor)]">
                <Icon name={TAB_ICONS[tab]} size={22} aria-hidden={true} />
              </span>

              <div>
                <div className="text-[15px] font-medium text-[var(--textColor)]">
                  {translate('NothingToShow')}
                </div>
                <div className="mt-[4px] text-[13px] text-[var(--disabledColor)]">
                  {translate('ActivityDescription')}
                </div>
              </div>
            </div>
          ) : null}

          {canSelect ? (
            <div className={SELECTION_BAR_CLASS}>
              <span className="text-[13px] font-medium text-[var(--textColor)]">
                {translate('SelectedCount', { count: selectedCount })}
              </span>

              <div className="flex flex-wrap items-center gap-[8px]">
                {tab === 'queue' ? (
                  <button
                    type="button"
                    className={SELECTION_BUTTON_CLASS}
                    disabled={
                      !hasSelection || !isAnySelectedPending || isGrabbing
                    }
                    onClick={handleGrabSelectedPress}
                  >
                    <Icon name={icons.DOWNLOAD} size={12} aria-hidden={true} />
                    {translate('GrabSelected')}
                  </button>
                ) : null}

                <button
                  type="button"
                  className={SELECTION_BUTTON_CLASS}
                  disabled={!hasSelection || isRemoving || isRemovingBlocklist}
                  onClick={handleRemoveSelectedPress}
                >
                  <Icon name={icons.REMOVE} size={12} aria-hidden={true} />
                  {translate('RemoveSelected')}
                </button>

                {tab === 'queue' ? (
                  <button
                    type="button"
                    className={SELECTION_BUTTON_CLASS}
                    disabled={!hasSelection || !selectedDownloadIds.length}
                    onClick={handleImportSelectedPress}
                  >
                    <Icon
                      name={icons.INTERACTIVE}
                      size={12}
                      aria-hidden={true}
                    />
                    {translate('ImportSelected')}
                  </button>
                ) : null}

                <button
                  type="button"
                  className={SELECTION_BUTTON_CLASS}
                  disabled={!hasSelection}
                  onClick={clearSelection}
                >
                  {translate('Clear')}
                </button>
              </div>
            </div>
          ) : null}

          {pageItems.length ? (
            <Table
              className={ACTIVITY_TABLE_CLASS}
              columns={options.columns}
              sortKey={options.sortKey}
              sortDirection={options.sortDirection as SortDirection}
              pageSize={pageSize}
              canModifyColumns={true}
              selectAll={canSelect}
              allSelected={allSelected}
              allUnselected={allUnselected}
              onSortPress={handleSortPress}
              onTableOptionChange={handleTableOptionChange}
              onSelectAllChange={canSelect ? handleSelectAllChange : undefined}
            >
              <TableBody>
                {tab === 'queue'
                  ? (pageItems as MediaQueueItem[]).map((item) => (
                      <MediaQueueRow
                        key={item.key}
                        item={item}
                        columns={options.columns}
                        isSelected={selectedKeys.has(item.key)}
                        onSelectedChange={
                          canSelect ? handleSelectedChange : undefined
                        }
                        onModalOpenOrClose={handleQueueRowModalOpenOrClose}
                      />
                    ))
                  : null}

                {tab === 'history'
                  ? (pageItems as MediaHistoryItem[]).map((item) => (
                      <MediaHistoryRow
                        key={item.key}
                        item={item}
                        columns={options.columns}
                      />
                    ))
                  : null}

                {tab === 'blocklist'
                  ? (pageItems as MediaBlocklistItem[]).map((item) => (
                      <MediaBlocklistRow
                        key={item.key}
                        item={item}
                        columns={options.columns}
                        isSelected={selectedKeys.has(item.key)}
                        onSelectedChange={
                          canSelect ? handleSelectedChange : undefined
                        }
                      />
                    ))
                  : null}
              </TableBody>
            </Table>
          ) : null}

          {totalPages > 1 ? (
            <div className="flex flex-wrap items-center justify-between gap-[12px] border-t border-[var(--defaultBorderColor)] px-[16px] py-[10px] text-[12px] text-[var(--disabledColor)]">
              <span>
                {translate('ShowingItems', {
                  start: rangeStart,
                  end: rangeEnd,
                  total: searchedData.length,
                })}
              </span>

              <div className="flex items-center gap-[8px]">
                <button
                  type="button"
                  className={PAGER_BUTTON_CLASS}
                  disabled={currentPage <= 1}
                  onClick={handlePreviousPagePress}
                >
                  {translate('Previous')}
                </button>

                <span className="tabular-nums">
                  {currentPage} / {totalPages}
                </span>

                <button
                  type="button"
                  className={PAGER_BUTTON_CLASS}
                  disabled={currentPage >= totalPages}
                  onClick={handleNextPagePress}
                >
                  {translate('Next')}
                </button>
              </div>
            </div>
          ) : null}
        </div>
      </PageContentBody>

      <RemoveQueueItemModal
        isOpen={tab === 'queue' && isConfirmRemoveModalOpen}
        selectedCount={selectedCount}
        canChangeCategory={canBulkChangeCategory}
        canIgnore={canBulkIgnore}
        isPending={isBulkPending}
        downloadClient={queueSelected[0]?.downloadClient}
        onRemovePress={handleRemoveSelectedConfirmed}
        onModalClose={handleConfirmRemoveModalClose}
      />

      <ConfirmModal
        isOpen={tab === 'blocklist' && isConfirmRemoveModalOpen}
        kind={kinds.DANGER}
        title={translate('RemoveSelected')}
        message={translate('RemoveSelectedBlocklistMessageText')}
        confirmLabel={translate('RemoveSelected')}
        onConfirm={handleRemoveSelectedConfirmed}
        onCancel={handleConfirmRemoveModalClose}
      />

      <ConfirmModal
        isOpen={isConfirmClearModalOpen}
        kind={kinds.DANGER}
        title={translate('ClearBlocklist')}
        message={translate('ClearBlocklistMessageText')}
        confirmLabel={translate('Clear')}
        onConfirm={handleClearBlocklistConfirmed}
        onCancel={handleConfirmClearModalClose}
      />

      <InteractiveImportModal
        isOpen={isInteractiveImportOpen}
        downloadIds={selectedDownloadIds}
        title={translate('InteractiveImportMultipleQueueItems')}
        onModalClose={handleInteractiveImportModalClose}
      />
    </PageContent>
  );
}

export default MediaActivityPage;
