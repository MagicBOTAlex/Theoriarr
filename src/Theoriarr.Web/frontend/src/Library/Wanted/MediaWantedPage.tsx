import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import QueueDetailsProvider from 'Activity/Queue/Details/QueueDetailsProvider';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { EpisodeEntity } from 'Episode/useEpisode';
import { icons, kinds, sizes } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import translate from 'Utilities/String/translate';
import { MediaWantedItem, MediaWantedType } from './mediaWanted';
import MediaWantedRow from './MediaWantedRow';
import useMediaWanted, { WantedTab } from './useMediaWanted';

const TABS: { key: WantedTab; title: () => string; to: string }[] = [
  { key: 'missing', title: () => translate('Missing'), to: '/wanted/missing' },
  {
    key: 'cutoff',
    title: () => translate('CutoffUnmet'),
    to: '/wanted/cutoffunmet',
  },
];

const TYPE_FILTERS: { value: MediaWantedType | 'all'; label: () => string }[] =
  [
    { value: 'all', label: () => translate('All') },
    { value: 'series', label: () => translate('Series') },
    { value: 'movie', label: () => translate('Movies') },
  ];

const MONITORED_FILTERS = [
  { value: 'all', label: () => translate('All') },
  { value: 'monitored', label: () => translate('Monitored') },
  { value: 'unmonitored', label: () => translate('Unmonitored') },
] as const;

type MonitoredFilter = (typeof MONITORED_FILTERS)[number]['value'];

const COLUMNS: Column[] = [
  { name: 'type', label: 'Type', isVisible: true, isSortable: false },
  { name: 'title', label: () => translate('Title'), isVisible: true },
  { name: 'details', label: 'Details', isVisible: true, isSortable: false },
  { name: 'date', label: () => translate('Date'), isVisible: true },
  { name: 'monitored', label: () => translate('Monitored'), isVisible: true },
  { name: 'status', label: () => translate('Status'), isVisible: true },
  { name: 'actions', label: '', isVisible: true, isSortable: false },
];

const PAGE_SIZE = 50;

function getTabFromPath(pathname: string): WantedTab {
  return pathname.startsWith('/wanted/cutoffunmet') ? 'cutoff' : 'missing';
}

function getEpisodeEntity(tab: WantedTab): EpisodeEntity {
  return tab === 'cutoff' ? 'wanted.cutoffUnmet' : 'wanted.missing';
}

interface FilterButtonProps {
  value: string;
  isActive: boolean;
  onSelect: (value: string) => void;
  children: React.ReactNode;
}

function FilterButton({
  value,
  isActive,
  onSelect,
  children,
}: FilterButtonProps) {
  const handlePress = useCallback(() => {
    onSelect(value);
  }, [onSelect, value]);

  return (
    <Button
      className="mr-[6px]"
      kind={isActive ? kinds.PRIMARY : kinds.DEFAULT}
      size={sizes.SMALL}
      onPress={handlePress}
    >
      {children}
    </Button>
  );
}

function MediaWantedPage() {
  const { pathname } = useLocation();
  const tab = getTabFromPath(pathname);
  const { items, isLoading, error } = useMediaWanted(tab);

  const [typeFilter, setTypeFilter] = useState<MediaWantedType | 'all'>('all');
  const [monitoredFilter, setMonitoredFilter] =
    useState<MonitoredFilter>('all');
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<'title' | 'date'>('title');
  const [sortDirection, setSortDirection] =
    useState<SortDirection>('ascending');
  const [page, setPage] = useState(1);

  const executeCommand = useExecuteCommand();

  const episodeIds = useMemo(
    () => items.filter((item) => item.type === 'series').map((item) => item.id),
    [items]
  );

  useEffect(() => {
    setPage(1);
  }, [tab, typeFilter, monitoredFilter, search]);

  const handleTypeFilterSelect = useCallback((value: string) => {
    setTypeFilter(value as MediaWantedType | 'all');
  }, []);

  const handleSearchChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setSearch(event.target.value);
    },
    []
  );

  const handleMonitoredChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setMonitoredFilter(event.target.value as MonitoredFilter);
    },
    []
  );

  const handleSortPress = useCallback(
    (name: string, direction?: SortDirection) => {
      if (name !== 'title' && name !== 'date') {
        return;
      }

      let nextDirection: SortDirection = 'ascending';

      if (direction) {
        nextDirection = direction;
      } else if (name === sortKey && sortDirection === 'ascending') {
        nextDirection = 'descending';
      }

      setSortKey(name);
      setSortDirection(nextDirection);
    },
    [sortKey, sortDirection]
  );

  const handleSearchAllPress = useCallback(() => {
    if (tab === 'cutoff') {
      executeCommand({ name: CommandNames.CutoffUnmetEpisodeSearch });
      executeCommand({ name: 'CutoffUnmetMoviesSearch' });
    } else {
      executeCommand({ name: CommandNames.MissingEpisodeSearch });
      executeCommand({ name: 'MissingMoviesSearch' });
    }
  }, [tab, executeCommand]);

  const filteredItems = useMemo(() => {
    const term = search.trim().toLowerCase();

    return items
      .filter((item) => {
        if (typeFilter !== 'all' && item.type !== typeFilter) {
          return false;
        }

        if (monitoredFilter === 'monitored' && !item.monitored) {
          return false;
        }

        if (monitoredFilter === 'unmonitored' && item.monitored) {
          return false;
        }

        if (term && !item.title.toLowerCase().includes(term)) {
          return false;
        }

        return true;
      })
      .sort((a, b) => {
        const direction = sortDirection === 'descending' ? -1 : 1;

        if (sortKey === 'date') {
          const aValue = a.date ? Date.parse(a.date) : 0;
          const bValue = b.date ? Date.parse(b.date) : 0;
          return (aValue - bValue) * direction;
        }

        return (
          a.title.localeCompare(b.title, undefined, { numeric: true }) *
          direction
        );
      }) as MediaWantedItem[];
  }, [items, typeFilter, monitoredFilter, search, sortKey, sortDirection]);

  const totalPages = Math.max(Math.ceil(filteredItems.length / PAGE_SIZE), 1);
  const currentPage = Math.min(page, totalPages);
  const pageItems = filteredItems.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  const handlePreviousPagePress = useCallback(() => {
    setPage((previous) => Math.max(previous - 1, 1));
  }, []);

  const handleNextPagePress = useCallback(() => {
    setPage((previous) => previous + 1);
  }, []);

  return (
    <PageContent title={translate('Wanted')}>
      <PageToolbar>
        <PageToolbarSection alignContent="left">
          <PageToolbarButton
            label={translate('SearchAll')}
            iconName={icons.SEARCH}
            onPress={handleSearchAllPress}
          />
        </PageToolbarSection>
      </PageToolbar>

      <QueueDetailsProvider episodeIds={episodeIds}>
        <PageContentBody>
          <div className="flex h-full flex-col">
            <div className="flex flex-wrap items-center gap-3 border-b border-[var(--defaultBorderColor)] px-5 py-4">
              <div className="flex items-center gap-3">
                {TABS.map((option) => (
                  <Link
                    key={option.key}
                    className={
                      option.key === tab
                        ? 'border-b-2 border-b-[var(--themeBlue)] px-[2px] py-1 text-[14px] font-medium text-[var(--textColor)]'
                        : 'border-b-2 border-transparent px-[2px] py-1 text-[14px] font-medium text-[var(--disabledColor)]'
                    }
                    to={option.to}
                  >
                    {option.title()}
                  </Link>
                ))}
              </div>

              <div className="text-[13px] text-[var(--disabledColor)]">
                {filteredItems.length}
              </div>

              <div className="flex items-center">
                {TYPE_FILTERS.map((option) => (
                  <FilterButton
                    key={option.value}
                    value={option.value}
                    isActive={typeFilter === option.value}
                    onSelect={handleTypeFilterSelect}
                  >
                    {option.label()}
                  </FilterButton>
                ))}
              </div>

              <select
                className="rounded border border-[var(--inputBorderColor)] border-[var(--inputBorderColorVisible)] bg-[var(--inputBackgroundColor)] px-[10px] py-[6px] text-[13px] text-[var(--textColor)]"
                value={monitoredFilter}
                onChange={handleMonitoredChange}
              >
                {MONITORED_FILTERS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label()}
                  </option>
                ))}
              </select>

              <div className="flex-1" />

              <input
                className="min-w-[220px] rounded border border-[var(--inputBorderColor)] bg-[var(--inputBackgroundColor)] px-[10px] py-[6px] text-[13px] text-[var(--textColor)]"
                type="search"
                placeholder={translate('Search')}
                value={search}
                onChange={handleSearchChange}
              />
            </div>

            {isLoading && !items.length ? (
              <div className="p-5 text-[var(--disabledColor)]">
                {translate('Loading')}
              </div>
            ) : null}

            {error ? (
              <div className="p-5 text-[var(--dangerColor)]">
                {translate('WantedLoadError')}: {error.message}
              </div>
            ) : null}

            {!isLoading && !error && !pageItems.length ? (
              <div className="p-5 text-[var(--disabledColor)]">
                {translate('NothingToShow')}
              </div>
            ) : null}

            {pageItems.length ? (
              <Table
                columns={COLUMNS}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={handleSortPress}
              >
                <TableBody>
                  {pageItems.map((item) => (
                    <MediaWantedRow
                      key={item.key}
                      item={item}
                      episodeEntity={getEpisodeEntity(tab)}
                    />
                  ))}
                </TableBody>
              </Table>
            ) : null}

            {totalPages > 1 ? (
              <div className="flex items-center justify-center gap-3 p-4">
                <Button
                  size={sizes.SMALL}
                  isDisabled={currentPage <= 1}
                  onPress={handlePreviousPagePress}
                >
                  {translate('Previous')}
                </Button>

                <span className="text-[13px] text-[var(--disabledColor)]">
                  {currentPage} / {totalPages}
                </span>

                <Button
                  size={sizes.SMALL}
                  isDisabled={currentPage >= totalPages}
                  onPress={handleNextPagePress}
                >
                  {translate('Next')}
                </Button>
              </div>
            ) : null}
          </div>
        </PageContentBody>
      </QueueDetailsProvider>
    </PageContent>
  );
}

export default MediaWantedPage;
