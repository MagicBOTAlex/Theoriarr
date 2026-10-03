import React from 'react';
import HeartRating from 'Components/HeartRating';
import MovieTagList from 'Components/MovieTagList';
import SeriesTagList from 'Components/SeriesTagList';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import { MediaItem } from '../MediaItem';

const INFO_CLASS = 'bg-[var(--seriesBackgroundColor)] text-center text-[12px]';
const TAGS_CLASS =
  'flex items-center justify-around px-[3px] h-[21px] bg-[var(--seriesBackgroundColor)]';
const TAGS_LIST_CLASS = 'flex overflow-hidden';

interface LibraryIndexPosterInfoProps {
  item: MediaItem;
  qualityProfileName?: string;
  showQualityProfile: boolean;
  sortKey: string;
  showRelativeDates: boolean;
  shortDateFormat: string;
  longDateFormat: string;
  timeFormat: string;
  showTags: boolean;
}

function LibraryIndexPosterInfo(props: LibraryIndexPosterInfoProps) {
  const {
    item,
    qualityProfileName,
    showQualityProfile,
    sortKey,
    showRelativeDates,
    shortDateFormat,
    longDateFormat,
    timeFormat,
    showTags,
  } = props;

  const relativeDateProps = {
    shortDateFormat,
    showRelativeDates,
    timeFormat,
    timeForToday: true,
  };

  const tags = item.series?.tags ?? item.movie?.tags ?? [];

  if (sortKey === 'network' && item.series?.network) {
    return (
      <div className={INFO_CLASS} title={translate('Network')}>
        {item.series.network}
      </div>
    );
  }

  if (sortKey === 'studio' && item.movie?.studio) {
    return (
      <div className={INFO_CLASS} title={translate('Studio')}>
        {item.movie.studio}
      </div>
    );
  }

  if (sortKey === 'collection' && item.movie?.collection?.title) {
    return (
      <div className={INFO_CLASS} title={translate('Collection')}>
        {item.movie.collection.title}
      </div>
    );
  }

  if (sortKey === 'originalLanguage') {
    const language =
      item.series?.originalLanguage ?? item.movie?.originalLanguage;

    if (language?.name) {
      return (
        <div className={INFO_CLASS} title={translate('OriginalLanguage')}>
          {language.name}
        </div>
      );
    }
  }

  if (
    sortKey === 'qualityProfileId' &&
    !showQualityProfile &&
    qualityProfileName
  ) {
    return (
      <div className={INFO_CLASS} title={translate('QualityProfile')}>
        {qualityProfileName}
      </div>
    );
  }

  if (sortKey === 'previousAiring' && item.series?.previousAiring) {
    return (
      <div
        className={INFO_CLASS}
        title={`${translate('PreviousAiring')}: ${formatDateTime(
          item.series.previousAiring,
          longDateFormat,
          timeFormat
        )}`}
      >
        {getRelativeDate({
          date: item.series.previousAiring,
          ...relativeDateProps,
        })}
      </div>
    );
  }

  if (sortKey === 'added' && item.added) {
    return (
      <div
        className={INFO_CLASS}
        title={formatDateTime(item.added, longDateFormat, timeFormat)}
      >
        {translate('Added')}:{' '}
        {getRelativeDate({
          date: item.added,
          ...relativeDateProps,
          timeForToday: false,
        })}
      </div>
    );
  }

  if (sortKey === 'seasonCount' && item.type === 'series') {
    const seasonCount = item.series?.statistics?.seasonCount ?? 0;
    let seasons = translate('OneSeason');

    if (seasonCount === 0) {
      seasons = translate('NoSeasons');
    } else if (seasonCount > 1) {
      seasons = translate('CountSeasons', { count: seasonCount });
    }

    return <div className={INFO_CLASS}>{seasons}</div>;
  }

  if (sortKey === 'runtime' && item.movie?.runtime) {
    const minutes = item.movie.runtime;

    return (
      <div className={INFO_CLASS} title={translate('Runtime')}>
        {Math.floor(minutes / 60)}h {minutes % 60}m
      </div>
    );
  }

  if (sortKey === 'releaseDate' && item.movie?.releaseDate) {
    return (
      <div className={INFO_CLASS} title={translate('ReleaseDate')}>
        {formatDateTime(item.movie.releaseDate, longDateFormat, timeFormat)}
      </div>
    );
  }

  const releaseSortKeys = {
    inCinemas: {
      date: item.movie?.inCinemas,
      title: translate('InCinemas'),
    },
    digitalRelease: {
      date: item.movie?.digitalRelease,
      title: translate('DigitalRelease'),
    },
    physicalRelease: {
      date: item.movie?.physicalRelease,
      title: translate('PhysicalRelease'),
    },
  } as Record<string, { date?: string; title: string }>;

  const releaseSort = releaseSortKeys[sortKey];

  if (releaseSort?.date) {
    return (
      <div className={INFO_CLASS} title={releaseSort.title}>
        {formatDateTime(releaseSort.date, longDateFormat, timeFormat)}
      </div>
    );
  }

  if (!showTags && sortKey === 'tags' && tags.length) {
    return (
      <div className={TAGS_CLASS}>
        <div className={TAGS_LIST_CLASS}>
          {item.type === 'series' ? (
            <SeriesTagList tags={tags} />
          ) : (
            <MovieTagList tags={tags} />
          )}
        </div>
      </div>
    );
  }

  if (sortKey === 'path' && item.movie?.path) {
    return (
      <div className={INFO_CLASS} title={translate('Path')}>
        {item.movie.path}
      </div>
    );
  }

  if (sortKey === 'sizeOnDisk') {
    return (
      <div className={INFO_CLASS} title={translate('SizeOnDisk')}>
        {formatBytes(item.sizeOnDisk ?? 0)}
      </div>
    );
  }

  if (sortKey === 'ratings' && item.rating) {
    return (
      <div className={INFO_CLASS} title={translate('Rating')}>
        <HeartRating rating={item.rating} />
      </div>
    );
  }

  return null;
}

export default LibraryIndexPosterInfo;
