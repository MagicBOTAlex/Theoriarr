import React, { useCallback, useEffect, useRef, useState } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageMenuButton from 'Components/Menu/PageMenuButton';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import { align, kinds, sizes } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import formatShortTimeSpan from 'Utilities/Date/formatShortTimeSpan';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import InteractiveSearchFilterModal from './InteractiveSearchFilterModal';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import InteractiveSearchRow from './InteractiveSearchRow';
import InteractiveSearchType from './InteractiveSearchType';
import { setReleaseOption, useReleaseOptions } from './releaseOptionsStore';
import ReleaseSearchProgress from './ReleaseSearchProgress';
import { useReleaseSearchProgress } from './releaseSearchProgressStore';
import useReleases, {
  FILTERS,
  MOVIE_FILTERS,
  setReleaseSort,
} from './useReleases';

interface InteractiveSearchProps {
  type: InteractiveSearchType;
  searchPayload: InteractiveSearchPayload;
}

// Subtle column separators for the release table. The shared cell class only draws the
// horizontal row divider, so the interactive search gets its own faint vertical rules and a
// muted header treatment.
const RELEASE_TABLE_CLASS =
  '[&_thead]:text-[11px] [&_thead]:uppercase [&_thead]:tracking-wider [&_thead]:text-[color-mix(in_srgb,var(--textColor),transparent_35%)] [&_th]:border-b [&_th]:border-solid [&_th]:border-[color-mix(in_srgb,var(--textColor),transparent_75%)] [&_td]:border-r [&_td]:border-solid [&_td]:border-[color-mix(in_srgb,var(--textColor),transparent_90%)] [&_td:last-child]:border-r-0 [&_td]:align-middle';

function InteractiveSearch({ type, searchPayload }: InteractiveSearchProps) {
  const customFilters = useCustomFiltersList('releases');
  const { columns } = useReleaseOptions();

  const {
    isFetching,
    isFetched,
    error,
    data,
    totalItems,
    selectedFilterKey,
    sortKey,
    sortDirection,
    searchId,
    refetch,
    cacheStatus,
    cacheDecision,
    loadCachedResults,
    searchAgain,
  } = useReleases(searchPayload);

  const [isProgressDismissed, setIsProgressDismissed] = useState(false);
  const hasRefetchedFinalResults = useRef(false);

  const searchProgress = useReleaseSearchProgress(searchId);

  const cacheAge =
    cacheStatus?.cachedAt != null
      ? formatShortTimeSpan(Date.now() - Date.parse(cacheStatus.cachedAt))
      : '';

  // The interactive search runs in the background: results arrive in batches as each indexer
  // finishes. The progress panel can be dismissed while the remaining indexers keep searching,
  // and the results can be pulled at any point with the refresh button.
  const isSearching = !!searchProgress && !searchProgress.isComplete && !error;
  const isSearchComplete = searchProgress?.isComplete ?? false;
  const indexerCount = searchProgress?.indexers.length ?? 0;
  const completedIndexerCount =
    searchProgress?.indexers.filter((indexer) => indexer.status !== 'searching')
      .length ?? 0;

  useEffect(() => {
    if (!isSearchComplete) {
      hasRefetchedFinalResults.current = false;

      return;
    }

    if (!hasRefetchedFinalResults.current) {
      hasRefetchedFinalResults.current = true;
      refetch();
    }
  }, [isSearchComplete, refetch]);

  const handleProgressDismiss = useCallback(() => {
    setIsProgressDismissed(true);
    // Loading whatever the finished indexers have returned so far, so skipping the panel
    // does not leave the user staring at stale results.
    refetch();
  }, [refetch]);

  const handleProgressShow = useCallback(() => {
    setIsProgressDismissed(false);
  }, []);

  const handleRefresh = useCallback(() => {
    refetch();
  }, [refetch]);

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      if (type === 'episode') {
        setReleaseOption('episodeSelectedFilterKey', selectedFilterKey);
      } else if (type === 'movie') {
        setReleaseOption('movieSelectedFilterKey', selectedFilterKey);
      } else {
        setReleaseOption('seasonSelectedFilterKey', selectedFilterKey);
      }
    },
    [type]
  );

  const handleSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      setReleaseSort(sortKey, sortDirection);
    },
    []
  );

  const errorMessage = getErrorMessage(error);

  return (
    <div>
      <div className="mb-[10px] flex justify-end">
        <FilterMenu
          alignMenu={align.RIGHT}
          selectedFilterKey={selectedFilterKey}
          filters={type === 'movie' ? MOVIE_FILTERS : FILTERS}
          customFilters={customFilters}
          buttonComponent={PageMenuButton}
          filterModalConnectorComponent={InteractiveSearchFilterModal}
          filterModalConnectorComponentProps={{ type, searchPayload }}
          onFilterSelect={handleFilterSelect}
        />
      </div>

      {cacheDecision === 'prompt' ? (
        <Alert kind={kinds.INFO}>
          <div className="flex flex-wrap items-center justify-between gap-[10px]">
            <span>
              {translate('InteractiveSearchCachedResults', { age: cacheAge })}
            </span>

            <span className="flex gap-2">
              <Button size={sizes.SMALL} onPress={loadCachedResults}>
                {translate('LoadCachedResults')}
              </Button>

              <Button size={sizes.SMALL} onPress={searchAgain}>
                {translate('SearchAgain')}
              </Button>
            </span>
          </div>
        </Alert>
      ) : null}

      {isSearching && !isProgressDismissed ? (
        <ReleaseSearchProgress
          searchId={searchId}
          isRefreshing={isFetching}
          onDismiss={handleProgressDismiss}
          onRefresh={handleRefresh}
        />
      ) : null}

      {isSearching && isProgressDismissed ? (
        <div className="mb-4 flex items-center gap-3 rounded-lg border border-[var(--borderColor)] bg-[color-mix(in_srgb,var(--textColor),transparent_97%)] px-3 py-2 text-sm">
          <span className="inline-block h-2 w-2 shrink-0 animate-pulse rounded-full bg-blue-500" />

          <span className="font-medium">
            {`${translate(
              'SearchingIndexers'
            )} (${completedIndexerCount}/${indexerCount})`}
          </span>

          <div className="ml-auto flex items-center gap-2">
            <Button
              size={sizes.SMALL}
              isDisabled={isFetching}
              onPress={handleRefresh}
            >
              {translate('Refresh')}
            </Button>

            <Button size={sizes.SMALL} onPress={handleProgressShow}>
              {translate('ShowProgress')}
            </Button>
          </div>
        </div>
      ) : null}

      {cacheDecision === 'pending' || (isFetching && !searchProgress) ? (
        <LoadingIndicator />
      ) : null}

      {!isFetching && error ? (
        <div>
          {errorMessage ? (
            <>
              {translate(
                type === 'movie'
                  ? 'InteractiveSearchResultsMovieFailedErrorMessage'
                  : 'InteractiveSearchResultsSeriesFailedErrorMessage',
                {
                  message:
                    errorMessage.charAt(0).toLowerCase() +
                    errorMessage.slice(1),
                }
              )}
            </>
          ) : (
            translate(
              type === 'movie'
                ? 'MovieSearchResultsLoadError'
                : 'EpisodeSearchResultsLoadError'
            )
          )}
        </div>
      ) : null}

      {!isFetching && isFetched && !totalItems && !isSearching ? (
        <Alert kind={kinds.INFO}>{translate('NoResultsFound')}</Alert>
      ) : null}

      {!!totalItems && !isFetching && !data.length ? (
        <Alert kind={kinds.WARNING}>
          {translate('AllResultsAreHiddenByTheAppliedFilter')}
        </Alert>
      ) : null}

      {data.length ? (
        <div className={RELEASE_TABLE_CLASS}>
          <Table
            columns={columns}
            sortKey={sortKey}
            sortDirection={sortDirection}
            onSortPress={handleSortPress}
          >
            <TableBody>
              {data.map((item) => {
                return (
                  <InteractiveSearchRow
                    key={`${item.release.indexerId}-${item.release.guid}`}
                    {...item}
                    type={type}
                    searchPayload={searchPayload}
                  />
                );
              })}
            </TableBody>
          </Table>
        </div>
      ) : null}

      {totalItems !== data.length && !!data.length ? (
        <div className="mt-[10px]">
          {translate('SomeResultsAreHiddenByTheAppliedFilter')}
        </div>
      ) : null}
    </div>
  );
}

export default InteractiveSearch;
