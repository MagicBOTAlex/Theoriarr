import classNames from 'classnames';
import React from 'react';
import {
  useQueueDetails,
  useQueueDetailsForSeries,
} from 'Activity/Queue/Details/QueueDetailsProvider';
import ProgressBar, {
  PROGRESS_BAR_CLASS,
  PROGRESS_BAR_CONTAINER_CLASS,
  ProgressBarPart,
} from 'Components/ProgressBar';
import { kinds, sizes } from 'Helpers/Props';
import { MovieStatus } from 'Movies/Movie';
import { SeriesStatus } from 'Series/Series';
import getMovieProgressBarKind from 'Utilities/Movie/getProgressBarKind';
import getSeriesProgressBarKind from 'Utilities/Series/getProgressBarKind';
import translate from 'Utilities/String/translate';
import { MediaItem } from '../MediaItem';

const LIBRARY_PROGRESS_BAR_CLASS = classNames(
  PROGRESS_BAR_CLASS,
  'transition-[width_200ms_ease]'
);

const LIBRARY_PROGRESS_CONTAINER_CLASS = classNames(
  PROGRESS_BAR_CONTAINER_CLASS,
  'rounded-none bg-[#5b5b5b]! text-[var(--white)]'
);

type ProgressBarKind = React.ComponentProps<typeof ProgressBar>['kind'];

interface LibraryIndexProgressBarProps {
  item: MediaItem;
  seasonNumber?: number;
  width: number;
  detailedProgressBar: boolean;
  isStandalone: boolean;
}

function LibraryIndexProgressBar(props: LibraryIndexProgressBarProps) {
  const { item, seasonNumber, width, detailedProgressBar, isStandalone } =
    props;

  const seriesQueueDetails = useQueueDetailsForSeries(item.id, seasonNumber);
  const queueDetails = useQueueDetails();

  const movieQueueCount = queueDetails.reduce((acc, queueItem) => {
    if (
      queueItem.trackedDownloadState === 'imported' ||
      queueItem.movieId !== item.id
    ) {
      return acc;
    }

    return acc + 1;
  }, 0);

  if (item.type === 'series') {
    const season =
      seasonNumber == null
        ? undefined
        : item.series?.seasons?.find(
            (seriesSeason) => seriesSeason.seasonNumber === seasonNumber
          );
    const statistics = season?.statistics ?? item.series?.statistics;
    const episodeCount = statistics?.episodeCount ?? 0;
    const episodeFileCount = statistics?.episodeFileCount ?? 0;
    const totalEpisodeCount = statistics?.totalEpisodeCount ?? 0;

    const downloadingCount = Math.max(
      0,
      seriesQueueDetails.count - seriesQueueDetails.episodesWithFiles
    );
    const missingCount = Math.max(
      0,
      episodeCount - episodeFileCount - downloadingCount
    );

    const progress = episodeCount
      ? (episodeFileCount / episodeCount) * 100
      : 100;
    const text = downloadingCount
      ? `${episodeFileCount} + ${downloadingCount} / ${episodeCount}`
      : `${episodeFileCount} / ${episodeCount}`;

    const parts: ProgressBarPart[] = episodeCount
      ? [
          {
            kind: kinds.SUCCESS,
            value: (episodeFileCount / episodeCount) * 100,
          },
          {
            kind: kinds.PURPLE,
            value: (downloadingCount / episodeCount) * 100,
          },
          {
            kind: kinds.DANGER,
            value: (missingCount / episodeCount) * 100,
          },
        ]
      : [];

    return (
      <ProgressBar
        className={LIBRARY_PROGRESS_BAR_CLASS}
        containerClassName={
          isStandalone ? undefined : LIBRARY_PROGRESS_CONTAINER_CLASS
        }
        progress={progress}
        kind={getSeriesProgressBarKind(
          item.status as SeriesStatus,
          item.monitored,
          progress,
          seriesQueueDetails.count > 0
        )}
        parts={parts}
        size={detailedProgressBar ? sizes.MEDIUM : sizes.SMALL}
        showText={detailedProgressBar}
        text={text}
        title={translate('SeriesProgressBarText', {
          episodeFileCount,
          episodeCount,
          totalEpisodeCount,
          downloadingCount,
          missingCount,
        })}
        width={width}
      />
    );
  }

  const movieStatus = translateMovieStatus(item, movieQueueCount);

  return (
    <ProgressBar
      className={LIBRARY_PROGRESS_BAR_CLASS}
      containerClassName={
        isStandalone ? undefined : LIBRARY_PROGRESS_CONTAINER_CLASS
      }
      progress={100}
      kind={
        getMovieProgressBarKind(
          item.status as MovieStatus,
          item.monitored,
          item.hasFile,
          item.movie?.isAvailable ?? false,
          movieQueueCount > 0
        ) as ProgressBarKind
      }
      size={detailedProgressBar ? sizes.MEDIUM : sizes.SMALL}
      showText={detailedProgressBar}
      text={movieQueueCount > 0 ? translate('Downloading') : movieStatus}
      width={width}
    />
  );
}

function translateMovieStatus(item: MediaItem, movieQueueCount: number) {
  if (movieQueueCount > 0) {
    return translate('Downloading');
  }

  if (item.hasFile) {
    return (
      item.movie?.movieFile?.quality?.quality?.name ?? translate('Downloaded')
    );
  }

  if (item.status === 'deleted') {
    return translate('Deleted');
  }

  if (item.movie?.isAvailable && !item.hasFile) {
    return translate('Missing');
  }

  return translate('NotAvailable');
}

export default LibraryIndexProgressBar;
