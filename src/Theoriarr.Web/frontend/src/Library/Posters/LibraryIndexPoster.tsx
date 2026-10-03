import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import Label from 'Components/Label';
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
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import translate from 'Utilities/String/translate';
import { useLibraryPosterOptions } from '../libraryOptionsStore';
import { MediaItem } from '../MediaItem';
import LibraryIndexProgressBar from '../ProgressBar/LibraryIndexProgressBar';
import LibraryIndexPosterSelect from '../Select/LibraryIndexPosterSelect';
import useLibraryIndexItem from '../useLibraryIndexItem';
import LibraryIndexPosterInfo from './LibraryIndexPosterInfo';

const TAGS_CLASS =
  'flex items-center justify-around px-[3px] h-[21px] bg-[var(--seriesBackgroundColor)]';
const TAGS_LIST_CLASS = 'flex overflow-hidden';
const TITLE_CLASS =
  'overflow-hidden! max-w-full bg-[var(--seriesBackgroundColor)] text-center text-ellipsis! whitespace-nowrap! text-[12px]';
const NEXT_AIRING_CLASS =
  'bg-[var(--seriesBackgroundColor)] text-center text-[12px]';

interface LibraryIndexPosterProps {
  item: MediaItem;
  sortKey: string;
  isSelectMode: boolean;
  posterWidth: number;
  posterHeight: number;
}

function LibraryIndexPoster(props: LibraryIndexPosterProps) {
  const { item, sortKey, isSelectMode, posterWidth, posterHeight } = props;

  const { qualityProfile, isRefreshing, isSearching } =
    useLibraryIndexItem(item);

  const {
    detailedProgressBar,
    showTitle,
    showMonitored,
    showQualityProfile,
    showCinemaRelease,
    showDigitalRelease,
    showPhysicalRelease,
    showReleaseDate,
    showTags,
    showSearchAction,
  } = useLibraryPosterOptions();

  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();

  const executeCommand = useExecuteCommand();
  const [hasPosterError, setHasPosterError] = useState(false);
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

  const onPosterLoadError = useCallback(() => {
    setHasPosterError(true);
  }, []);

  const onPosterLoad = useCallback(() => {
    setHasPosterError(false);
  }, []);

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

  const tags = item.series?.tags ?? item.movie?.tags ?? [];

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
  };

  const relativeDateProps = {
    shortDateFormat,
    showRelativeDates,
    timeFormat,
    timeForToday: true,
  };

  return (
    <div className="group transition-all duration-200 ease-in hover:z-[2] hover:shadow-[0_0_12px_var(--black)]">
      <div className="relative" title={item.title}>
        {isSelectMode ? <LibraryIndexPosterSelect item={item} /> : null}

        <Label className="absolute bottom-[10px] left-[10px] z-[3] rounded-[4px] bg-[#4f566f] text-[var(--white)] text-[12px] opacity-0 transition-[opacity] duration-0 group-hover:opacity-90 group-hover:duration-200 group-hover:delay-150">
          <SpinnerIconButton
            className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] hover:text-[#ccc]!"
            name={icons.REFRESH}
            title={translate('Refresh')}
            isSpinning={isRefreshing}
            tabIndex={-1}
            onPress={onRefreshPress}
          />

          {showSearchAction ? (
            <SpinnerIconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] hover:text-[#ccc]!"
              name={icons.SEARCH}
              title={translate('Search')}
              isSpinning={isSearching}
              tabIndex={-1}
              onPress={onSearchPress}
            />
          ) : null}

          {item.type === 'series' ? (
            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] hover:text-[#ccc]!"
              name={icons.EDIT}
              title={translate('EditSeries')}
              aria-label={translate('EditSeries')}
              tabIndex={-1}
              onPress={onEditSeriesPress}
            />
          ) : (
            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] hover:text-[#ccc]!"
              name={icons.EDIT}
              title={translate('EditMovie')}
              aria-label={translate('EditMovie')}
              tabIndex={-1}
              onPress={onEditMoviePress}
            />
          )}
        </Label>

        {item.status === 'ended' ? (
          <div
            className={classNames(
              'absolute top-0 right-0 z-[1] w-0 h-0 [border-width:0_25px_25px_0] border-solid text-[var(--white)]',
              '[border-color:transparent_var(--dangerColor)_transparent_transparent]'
            )}
            title={translate('Ended')}
          />
        ) : null}

        {item.status === 'deleted' ? (
          <div
            className={classNames(
              'absolute top-0 right-0 z-[1] w-0 h-0 [border-width:0_25px_25px_0] border-solid text-[var(--white)]',
              '[border-color:transparent_var(--gray)_transparent_transparent]'
            )}
            title={translate('Deleted')}
          />
        ) : null}

        <Link
          className="relative block h-[70px] bg-[var(--seriesBackgroundColor)]!"
          style={elementStyle}
          to={item.link}
        >
          {item.type === 'series' ? (
            <SeriesPoster
              style={elementStyle}
              images={item.series?.images ?? []}
              size={250}
              lazy={false}
              overflow={true}
              title={item.title}
              onError={onPosterLoadError}
              onLoad={onPosterLoad}
            />
          ) : (
            <MoviePoster
              style={elementStyle}
              images={item.movie?.images ?? []}
              size={250}
              lazy={false}
              overflow={true}
              alt={item.title}
              onError={onPosterLoadError}
              onLoad={onPosterLoad}
            />
          )}

          {hasPosterError ? (
            <div className="absolute top-0 left-0 flex items-center justify-center p-[5px] w-full h-full text-[var(--offWhite)] text-center text-[20px]">
              {item.title}
            </div>
          ) : null}
        </Link>
      </div>

      <LibraryIndexProgressBar
        item={item}
        width={posterWidth}
        detailedProgressBar={detailedProgressBar}
        isStandalone={false}
      />

      {showTitle ? (
        <div className={TITLE_CLASS} title={item.title}>
          {item.title}
        </div>
      ) : null}

      {showMonitored ? (
        <div className={TITLE_CLASS}>
          {item.monitored ? translate('Monitored') : translate('Unmonitored')}
        </div>
      ) : null}

      {showQualityProfile && !!qualityProfile?.name ? (
        <div className={TITLE_CLASS} title={translate('QualityProfile')}>
          {qualityProfile.name}
        </div>
      ) : null}

      {item.type === 'series' && item.series?.nextAiring ? (
        <div
          className={NEXT_AIRING_CLASS}
          title={`${translate('NextAiring')}: ${formatDateTime(
            item.series.nextAiring,
            longDateFormat,
            timeFormat
          )}`}
        >
          {getRelativeDate({
            date: item.series.nextAiring,
            ...relativeDateProps,
          })}
        </div>
      ) : null}

      {item.type === 'movie' && item.movie
        ? (
            [
              showCinemaRelease && item.movie.inCinemas
                ? [translate('InCinemas'), item.movie.inCinemas]
                : null,
              showDigitalRelease && item.movie.digitalRelease
                ? [translate('DigitalRelease'), item.movie.digitalRelease]
                : null,
              showPhysicalRelease && item.movie.physicalRelease
                ? [translate('PhysicalRelease'), item.movie.physicalRelease]
                : null,
              showReleaseDate && item.movie.releaseDate
                ? [translate('ReleaseDate'), item.movie.releaseDate]
                : null,
            ] as Array<[string, string] | null>
          ).map((release) => {
            if (!release) {
              return null;
            }

            return (
              <div
                key={release[0]}
                className={NEXT_AIRING_CLASS}
                title={formatDateTime(release[1], longDateFormat, timeFormat)}
              >
                {release[0]}:{' '}
                {getRelativeDate({
                  date: release[1],
                  ...relativeDateProps,
                })}
              </div>
            );
          })
        : null}

      {showTags && tags.length ? (
        <div className={TAGS_CLASS}>
          <div className={TAGS_LIST_CLASS}>
            {item.type === 'series' ? (
              <SeriesTagList tags={tags} />
            ) : (
              <MovieTagList tags={tags} />
            )}
          </div>
        </div>
      ) : null}

      <LibraryIndexPosterInfo
        item={item}
        qualityProfileName={qualityProfile?.name}
        showQualityProfile={showQualityProfile}
        sortKey={sortKey}
        showRelativeDates={showRelativeDates}
        shortDateFormat={shortDateFormat}
        longDateFormat={longDateFormat}
        timeFormat={timeFormat}
        showTags={showTags}
      />

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

export default LibraryIndexPoster;
