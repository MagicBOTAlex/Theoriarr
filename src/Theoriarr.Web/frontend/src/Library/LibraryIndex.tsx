import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useSearchParams } from 'react-router-dom';
import QueueDetailsProvider from 'Activity/Queue/Details/QueueDetailsProvider';
import { useAppDimension } from 'App/appStore';
import { SelectProvider } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody, {
  PAGE_CONTENT_BODY_CLASS,
  PAGE_CONTENT_BODY_INNER_CLASS,
} from 'Components/Page/PageContentBody';
import PageJumpBar, { PageJumpBarItems } from 'Components/Page/PageJumpBar';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import { align, icons, kinds } from 'Helpers/Props';
import { DESCENDING } from 'Helpers/Props/sortDirections';
import { TableOptionsChangePayload } from 'typings/Table';
import translate from 'Utilities/String/translate';
import { LIBRARY_FILTERS } from './libraryFilters';
import LibraryIndexFooter from './LibraryIndexFooter';
import LibraryIndexRefreshButton from './LibraryIndexRefreshButton';
import LibraryIndexSearchInput from './LibraryIndexSearchInput';
import {
  setLibraryOption,
  setLibrarySort,
  setLibraryTableOptions,
  useLibraryOption,
  useLibraryOptions,
} from './libraryOptionsStore';
import { MediaItem } from './MediaItem';
import LibraryIndexFilterMenu from './Menus/LibraryIndexFilterMenu';
import LibraryIndexSortMenu from './Menus/LibraryIndexSortMenu';
import LibraryIndexViewMenu from './Menus/LibraryIndexViewMenu';
import LibraryIndexOverviews from './Overview/LibraryIndexOverviews';
import LibraryIndexOverviewOptionsModal from './Overview/Options/LibraryIndexOverviewOptionsModal';
import LibraryIndexPosters from './Posters/LibraryIndexPosters';
import LibraryIndexPosterOptionsModal from './Posters/Options/LibraryIndexPosterOptionsModal';
import LibraryIndexSelectAllButton from './Select/LibraryIndexSelectAllButton';
import LibraryIndexSelectAllMenuItem from './Select/LibraryIndexSelectAllMenuItem';
import LibraryIndexSelectFooter from './Select/LibraryIndexSelectFooter';
import LibraryIndexSelectModeButton from './Select/LibraryIndexSelectModeButton';
import LibraryIndexSelectModeMenuItem from './Select/LibraryIndexSelectModeMenuItem';
import LibraryIndexTable from './Table/LibraryIndexTable';
import LibraryIndexTableOptions from './Table/LibraryIndexTableOptions';
import useLibraryIndex, { useLibraryCustomFilters } from './useLibraryIndex';
import useLibrarySearch from './useLibrarySearch';

const PAGE_CONTENT_BODY_WRAPPER_CLASS =
  'flex flex-[1_0_1px] overflow-hidden max-[768px]:basis-auto!';

const CONTENT_BODY_CLASS = `${PAGE_CONTENT_BODY_CLASS} relative flex flex-col max-[768px]:flex-[1_0_1px]!`;

const INNER_CONTENT_BODY_CLASSES: Record<string, string | undefined> = {
  posters: 'flex flex-col grow p-[15px] max-[768px]:p-[5px]',
  table: `${PAGE_CONTENT_BODY_INNER_CLASS} flex flex-col grow`,
};

const CONTENT_BODY_CONTAINER_CLASS = 'flex flex-col grow';

function getViewComponent(view: string) {
  if (view === 'posters') {
    return LibraryIndexPosters;
  }

  if (view === 'overview') {
    return LibraryIndexOverviews;
  }

  return LibraryIndexTable;
}

const TYPE_FILTERS: Record<string, string> = {
  series: 'seriesOnly',
  movie: 'movieOnly',
};

function LibraryIndex() {
  const { data, isLoading, isFetched, isError, error } = useLibraryIndex();

  const { selectedFilterKey, sortKey, sortDirection, view, columns } =
    useLibraryOptions();
  const filters = LIBRARY_FILTERS;
  const customFilters = useLibraryCustomFilters();

  const executeCommand = useExecuteCommand();
  const isRssSyncExecuting = useCommandExecuting(CommandNames.RssSync);
  const isSmallScreen = useAppDimension('isSmallScreen');
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [searchParams] = useSearchParams();
  const [isOptionsModalOpen, setIsOptionsModalOpen] = useState(false);
  const [jumpToCharacter, setJumpToCharacter] = useState<string | undefined>(
    undefined
  );
  const [isSelectMode, setIsSelectMode] = useState(false);
  const search = useLibraryOption('search');
  const { results: searchResults, isSearching } = useLibrarySearch(search);

  const itemByKey = useMemo(() => {
    const map = new Map<string, MediaItem>();

    data.forEach((item) => {
      map.set(item.selectKey, item);
    });

    return map;
  }, [data]);

  const filteredData = useMemo(() => {
    const term = search.trim();

    if (!term) {
      return data;
    }

    return searchResults
      .map((result) => itemByKey.get(`${result.mediaType}-${result.id}`))
      .filter((item): item is MediaItem => item != null);
  }, [data, itemByKey, search, searchResults]);

  const selectItems = useMemo(
    () => filteredData.map((item) => ({ id: item.selectKey })),
    [filteredData]
  );

  const handleSearchChange = useCallback((value: string) => {
    setLibraryOption('search', value);
  }, []);

  useEffect(() => {
    const typeParam = searchParams.get('type');
    const mappedFilter = typeParam ? TYPE_FILTERS[typeParam] : undefined;

    if (mappedFilter) {
      setLibraryOption('selectedFilterKey', mappedFilter);
    }
  }, [searchParams]);

  const onRssSyncPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RssSync,
    });
  }, [executeCommand]);

  const onSelectModePress = useCallback(() => {
    setIsSelectMode((value) => !value);
  }, []);

  const onTableOptionChange = useCallback(
    (
      payload: TableOptionsChangePayload & {
        tableOptions?: { showBanners?: boolean; showSearchAction?: boolean };
      }
    ) => {
      if (payload.tableOptions) {
        setLibraryTableOptions(payload.tableOptions);
      } else if (payload.columns) {
        setLibraryOption('columns', payload.columns);
      }
    },
    []
  );

  const onViewSelect = useCallback(
    (value: string) => {
      setLibraryOption('view', value);

      if (scrollerRef.current) {
        scrollerRef.current.scrollTo(0, 0);
      }
    },
    [scrollerRef]
  );

  const onSortSelect = useCallback((value: string) => {
    setLibrarySort({ sortKey: value });
  }, []);

  const onFilterSelect = useCallback((value: string | number) => {
    setLibraryOption('selectedFilterKey', value);
  }, []);

  const onOptionsPress = useCallback(() => setIsOptionsModalOpen(true), []);
  const onOptionsModalClose = useCallback(
    () => setIsOptionsModalOpen(false),
    []
  );

  const onJumpBarItemPress = useCallback((character: string) => {
    setJumpToCharacter(character);
  }, []);

  const onScroll = useCallback(() => {
    setJumpToCharacter(undefined);
  }, []);

  const jumpBarItems: PageJumpBarItems = useMemo(() => {
    if (sortKey !== 'sortTitle') {
      return { characters: {}, order: [] };
    }

    const characters = filteredData.reduce(
      (acc: Record<string, number>, item) => {
        let char = item.sortTitle.charAt(0);

        if (!isNaN(Number(char))) {
          char = '#';
        }

        if (char in acc) {
          acc[char] = acc[char] + 1;
        } else {
          acc[char] = 1;
        }

        return acc;
      },
      {}
    );

    const order = Object.keys(characters).sort();

    if (sortDirection === DESCENDING) {
      order.reverse();
    }

    return { characters, order };
  }, [filteredData, sortKey, sortDirection]);

  const ViewComponent = useMemo(() => getViewComponent(view), [view]);

  const isLoaded = !!(!isError && isFetched && filteredData.length);
  const hasNoItems = !filteredData.length;

  return (
    <QueueDetailsProvider all={true}>
      <SelectProvider items={selectItems}>
        <PageContent title={translate('Library')}>
          <PageToolbar>
            <PageToolbarSection>
              <LibraryIndexRefreshButton
                isSelectMode={isSelectMode}
                selectedFilterKey={selectedFilterKey}
              />

              <PageToolbarButton
                label={translate('RssSync')}
                iconName={icons.RSS}
                isSpinning={isRssSyncExecuting}
                isDisabled={hasNoItems}
                onPress={onRssSyncPress}
              />

              <PageToolbarSeparator />

              <LibraryIndexSelectModeButton
                label={
                  isSelectMode
                    ? translate('StopSelecting')
                    : translate('SelectItem')
                }
                iconName={isSelectMode ? icons.SERIES_ENDED : icons.CHECK}
                isSelectMode={isSelectMode}
                overflowComponent={LibraryIndexSelectModeMenuItem}
                onPress={onSelectModePress}
              />

              <LibraryIndexSelectAllButton
                label="SelectAll"
                isSelectMode={isSelectMode}
                overflowComponent={LibraryIndexSelectAllMenuItem}
              />
            </PageToolbarSection>

            <div className="flex flex-none items-center px-[10px]">
              <LibraryIndexSearchInput
                value={search}
                onChange={handleSearchChange}
              />
            </div>

            <PageToolbarSection
              alignContent={align.RIGHT}
              collapseButtons={false}
            >
              {view === 'table' ? (
                <TableOptionsModalWrapper
                  columns={columns}
                  optionsComponent={LibraryIndexTableOptions}
                  onTableOptionChange={onTableOptionChange}
                >
                  <PageToolbarButton
                    label={translate('Options')}
                    iconName={icons.TABLE}
                  />
                </TableOptionsModalWrapper>
              ) : (
                <PageToolbarButton
                  label={translate('Options')}
                  iconName={view === 'posters' ? icons.POSTER : icons.OVERVIEW}
                  isDisabled={hasNoItems}
                  onPress={onOptionsPress}
                />
              )}

              <PageToolbarSeparator />

              <LibraryIndexViewMenu
                view={view}
                isDisabled={hasNoItems}
                onViewSelect={onViewSelect}
              />

              <LibraryIndexSortMenu
                sortKey={sortKey}
                sortDirection={sortDirection}
                isDisabled={hasNoItems}
                onSortSelect={onSortSelect}
              />

              <LibraryIndexFilterMenu
                selectedFilterKey={selectedFilterKey}
                filters={filters}
                customFilters={customFilters}
                isDisabled={hasNoItems}
                onFilterSelect={onFilterSelect}
              />
            </PageToolbarSection>
          </PageToolbar>

          <div className={PAGE_CONTENT_BODY_WRAPPER_CLASS}>
            <PageContentBody
              ref={scrollerRef}
              className={CONTENT_BODY_CLASS}
              innerClassName={INNER_CONTENT_BODY_CLASSES[view]}
              scrollPositionKey="libraryIndex"
              onScroll={onScroll}
            >
              {isLoading && !isFetched ? <LoadingIndicator /> : null}

              {!isLoading && isError ? (
                <Alert kind={kinds.DANGER}>
                  {translate('LibraryLoadError')}
                  {error?.message ? `: ${error.message}` : ''}
                </Alert>
              ) : null}

              {isLoaded ? (
                <div className={CONTENT_BODY_CONTAINER_CLASS}>
                  <ViewComponent
                    scrollerRef={scrollerRef}
                    items={filteredData}
                    sortKey={sortKey}
                    sortDirection={sortDirection}
                    jumpToCharacter={jumpToCharacter}
                    isSelectMode={isSelectMode}
                    isSmallScreen={isSmallScreen}
                  />

                  <LibraryIndexFooter />
                </div>
              ) : null}

              {isSearching ? <LoadingIndicator /> : null}

              {!isError && isFetched && !filteredData.length && !isSearching ? (
                <Alert kind={kinds.INFO}>{translate('NothingToShow')}</Alert>
              ) : null}
            </PageContentBody>

            {isLoaded && !!jumpBarItems.order.length ? (
              <PageJumpBar
                items={jumpBarItems}
                onItemPress={onJumpBarItemPress}
              />
            ) : null}
          </div>

          {isSelectMode ? <LibraryIndexSelectFooter /> : null}

          {view === 'posters' ? (
            <LibraryIndexPosterOptionsModal
              isOpen={isOptionsModalOpen}
              onModalClose={onOptionsModalClose}
            />
          ) : null}
          {view === 'overview' ? (
            <LibraryIndexOverviewOptionsModal
              isOpen={isOptionsModalOpen}
              onModalClose={onOptionsModalClose}
            />
          ) : null}
        </PageContent>
      </SelectProvider>
    </QueueDetailsProvider>
  );
}

export default LibraryIndex;
