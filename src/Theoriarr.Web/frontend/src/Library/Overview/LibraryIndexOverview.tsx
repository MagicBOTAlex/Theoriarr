import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import TextTruncate from 'react-text-truncate';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import MovieTagList from 'Components/MovieTagList';
import SeriesTagList from 'Components/SeriesTagList';
import { icons } from 'Helpers/Props';
import DeleteMovieModal from 'Movies/Delete/DeleteMovieModal';
import EditMovieModal from 'Movies/Edit/EditMovieModal';
import MoviePoster from 'Movies/MoviePoster';
import DeleteSeriesModal from 'Series/Delete/DeleteSeriesModal';
import EditSeriesModal from 'Series/Edit/EditSeriesModal';
import SeriesPoster from 'Series/SeriesPoster';
import dimensions from 'Styles/Variables/dimensions';
import fonts from 'Styles/Variables/fonts';
import translate from 'Utilities/String/translate';
import { useLibraryOverviewOptions } from '../libraryOptionsStore';
import { MediaItem } from '../MediaItem';
import LibraryIndexProgressBar from '../ProgressBar/LibraryIndexProgressBar';
import LibraryIndexPosterSelect from '../Select/LibraryIndexPosterSelect';
import useLibraryIndexItem from '../useLibraryIndexItem';
import LibraryIndexOverviewInfo from './LibraryIndexOverviewInfo';

const STATUS_BASE_CLASS =
  'absolute top-0 right-0 z-[1] w-0 h-0 [border-width:0_25px_25px_0] border-solid text-[var(--white)]';

const columnPadding = parseInt(dimensions.seriesIndexColumnPadding);
const columnPaddingSmallScreen = parseInt(
  dimensions.seriesIndexColumnPaddingSmallScreen
);
const defaultFontSize = parseInt(fonts.defaultFontSize);
const lineHeight = parseFloat(fonts.lineHeight);

const TITLE_HEIGHT = 42;

interface LibraryIndexOverviewProps {
  item: MediaItem;
  sortKey: string;
  posterWidth: number;
  posterHeight: number;
  rowHeight: number;
  isSelectMode: boolean;
  isSmallScreen: boolean;
}

function LibraryIndexOverview(props: LibraryIndexOverviewProps) {
  const {
    item,
    sortKey,
    posterWidth,
    posterHeight,
    rowHeight,
    isSelectMode,
    isSmallScreen,
  } = props;

  const { qualityProfile, isRefreshing, isSearching } =
    useLibraryIndexItem(item);

  const overviewOptions = useLibraryOverviewOptions();

  const executeCommand = useExecuteCommand();
  const [isEditSeriesModalOpen, setIsEditSeriesModalOpen] = useState(false);
  const [isDeleteSeriesModalOpen, setIsDeleteSeriesModalOpen] = useState(false);
  const [isEditMovieModalOpen, setIsEditMovieModalOpen] = useState(false);
  const [isDeleteMovieModalOpen, setIsDeleteMovieModalOpen] = useState(false);

  const onRefreshPress = useCallback(() => {
    executeCommand(
      item.type === 'series'
        ? { name: CommandNames.RefreshSeries, seriesIds: [item.id] }
        : { name: CommandNames.RefreshMovie, movieIds: [item.id] }
    );
  }, [item.id, item.type, executeCommand]);

  const onSearchPress = useCallback(() => {
    executeCommand(
      item.type === 'series'
        ? { name: CommandNames.SeriesSearch, seriesId: item.id }
        : { name: CommandNames.MoviesSearch, movieIds: [item.id] }
    );
  }, [item.id, item.type, executeCommand]);

  const onEditSeriesPress = useCallback(() => {
    setIsEditSeriesModalOpen(true);
  }, []);

  const onEditSeriesModalClose = useCallback(() => {
    setIsEditSeriesModalOpen(false);
  }, []);

  const onDeleteSeriesPress = useCallback(() => {
    setIsEditSeriesModalOpen(false);
    setIsDeleteSeriesModalOpen(true);
  }, []);

  const onDeleteSeriesModalClose = useCallback(() => {
    setIsDeleteSeriesModalOpen(false);
  }, []);

  const onEditMoviePress = useCallback(() => {
    setIsEditMovieModalOpen(true);
  }, []);

  const onEditMovieModalClose = useCallback(() => {
    setIsEditMovieModalOpen(false);
  }, []);

  const onDeleteMoviePress = useCallback(() => {
    setIsEditMovieModalOpen(false);
    setIsDeleteMovieModalOpen(true);
  }, []);

  const onDeleteMovieModalClose = useCallback(() => {
    setIsDeleteMovieModalOpen(false);
  }, []);

  const contentHeight = useMemo(() => {
    const padding = isSmallScreen ? columnPaddingSmallScreen : columnPadding;

    return rowHeight - padding;
  }, [rowHeight, isSmallScreen]);

  const overviewHeight = contentHeight - TITLE_HEIGHT;

  const tags = item.series?.tags ?? item.movie?.tags ?? [];

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
  };

  return (
    <div>
      <div className="flex grow">
        <div className="relative">
          <div className="relative">
            {isSelectMode ? <LibraryIndexPosterSelect item={item} /> : null}

            {item.status === 'ended' ? (
              <div
                className={classNames(
                  STATUS_BASE_CLASS,
                  '[border-color:transparent_var(--dangerColor)_transparent_transparent]'
                )}
                title={translate('Ended')}
              />
            ) : null}

            {item.status === 'deleted' ? (
              <div
                className={classNames(
                  STATUS_BASE_CLASS,
                  '[border-color:transparent_var(--gray)_transparent_transparent]'
                )}
                title={translate('Deleted')}
              />
            ) : null}

            <Link
              className="block bg-[var(--seriesBackgroundColor)]! text-[var(--defaultColor)] hover:text-[var(--defaultColor)]! hover:no-underline!"
              style={elementStyle}
              to={item.link}
            >
              {item.type === 'series' ? (
                <SeriesPoster
                  className="relative"
                  style={elementStyle}
                  images={item.series?.images ?? []}
                  size={250}
                  lazy={false}
                  overflow={true}
                  title={item.title}
                />
              ) : (
                <MoviePoster
                  className="relative"
                  style={elementStyle}
                  images={item.movie?.images ?? []}
                  size={250}
                  lazy={false}
                  overflow={true}
                  alt={item.title}
                />
              )}
            </Link>
          </div>

          <LibraryIndexProgressBar
            item={item}
            width={posterWidth}
            detailedProgressBar={overviewOptions.detailedProgressBar}
            isStandalone={false}
          />
        </div>

        <div
          className="flex flex-[1_0_1px] flex-col overflow-hidden pl-[10px]"
          style={{ maxHeight: contentHeight }}
        >
          <div className="flex justify-between flex-[0_0_auto] mb-[10px] leading-[32px]">
            <Link
              className="block grow shrink-0 basis-[1px] overflow-hidden! max-w-full text-ellipsis! whitespace-nowrap! font-light text-[30px] text-[var(--defaultColor)] hover:text-[var(--defaultColor)]! hover:no-underline!"
              to={item.link}
            >
              {item.title}
            </Link>

            <div className="whitespace-nowrap">
              <SpinnerIconButton
                name={icons.REFRESH}
                title={translate('Refresh')}
                isSpinning={isRefreshing}
                onPress={onRefreshPress}
              />

              {overviewOptions.showSearchAction ? (
                <SpinnerIconButton
                  name={icons.SEARCH}
                  title={translate('Search')}
                  isSpinning={isSearching}
                  onPress={onSearchPress}
                />
              ) : null}

              {item.type === 'series' ? (
                <IconButton
                  name={icons.EDIT}
                  title={translate('EditSeries')}
                  aria-label={translate('EditSeries')}
                  onPress={onEditSeriesPress}
                />
              ) : (
                <IconButton
                  name={icons.EDIT}
                  title={translate('EditMovie')}
                  aria-label={translate('EditMovie')}
                  onPress={onEditMoviePress}
                />
              )}
            </div>
          </div>

          <div className="flex justify-between flex-[1_0_auto]">
            <div className="flex justify-between flex-[0_1_1000px] flex-col">
              <Link
                className="block min-h-0 overflow-hidden text-[var(--defaultColor)] hover:text-[var(--defaultColor)]! hover:no-underline! max-[768px]:hidden"
                to={item.link}
              >
                <TextTruncate
                  line={Math.floor(
                    overviewHeight / (defaultFontSize * lineHeight)
                  )}
                  text={item.overview}
                />
              </Link>

              {overviewOptions.showTags ? (
                <div className="flex justify-around overflow-hidden">
                  {item.type === 'series' ? (
                    <SeriesTagList tags={tags} />
                  ) : (
                    <MovieTagList tags={tags} />
                  )}
                </div>
              ) : null}
            </div>

            <LibraryIndexOverviewInfo
              height={overviewHeight}
              item={item}
              qualityProfile={qualityProfile}
              sortKey={sortKey}
              showNetwork={overviewOptions.showNetwork}
              showStudio={overviewOptions.showStudio}
              showCollection={overviewOptions.showCollection}
              showMonitored={overviewOptions.showMonitored}
              showQualityProfile={overviewOptions.showQualityProfile}
              showPreviousAiring={overviewOptions.showPreviousAiring}
              showAdded={overviewOptions.showAdded}
              showSeasonCount={overviewOptions.showSeasonCount}
              showCinemaRelease={overviewOptions.showCinemaRelease}
              showDigitalRelease={overviewOptions.showDigitalRelease}
              showPhysicalRelease={overviewOptions.showPhysicalRelease}
              showReleaseDate={overviewOptions.showReleaseDate}
              showRuntime={overviewOptions.showRuntime}
              showPath={overviewOptions.showPath}
              showSizeOnDisk={overviewOptions.showSizeOnDisk}
            />
          </div>
        </div>
      </div>

      {item.type === 'series' ? (
        <>
          <EditSeriesModal
            isOpen={isEditSeriesModalOpen}
            seriesId={item.id}
            onModalClose={onEditSeriesModalClose}
            onDeleteSeriesPress={onDeleteSeriesPress}
          />

          <DeleteSeriesModal
            isOpen={isDeleteSeriesModalOpen}
            seriesId={item.id}
            onModalClose={onDeleteSeriesModalClose}
          />
        </>
      ) : (
        <>
          <EditMovieModal
            isOpen={isEditMovieModalOpen}
            movieId={item.id}
            onModalClose={onEditMovieModalClose}
            onDeleteMoviePress={onDeleteMoviePress}
          />

          <DeleteMovieModal
            isOpen={isDeleteMovieModalOpen}
            movieId={item.id}
            onModalClose={onDeleteMovieModalClose}
          />
        </>
      )}
    </div>
  );
}

export default LibraryIndexOverview;
