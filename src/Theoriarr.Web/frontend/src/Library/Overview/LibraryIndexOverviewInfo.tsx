import React from 'react';
import { IconName } from 'Components/Icon';
import { icons } from 'Helpers/Props';
import { QualityProfileModel } from 'Settings/Profiles/Quality/useQualityProfiles';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import dimensions from 'Styles/Variables/dimensions';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import { MediaItem } from '../MediaItem';
import LibraryIndexOverviewInfoRow from './LibraryIndexOverviewInfoRow';

interface LibraryIndexOverviewInfoProps {
  height: number;
  item: MediaItem;
  qualityProfile?: QualityProfileModel;
  sortKey: string;
  showNetwork: boolean;
  showStudio: boolean;
  showCollection: boolean;
  showMonitored: boolean;
  showQualityProfile: boolean;
  showPreviousAiring: boolean;
  showAdded: boolean;
  showSeasonCount: boolean;
  showCinemaRelease: boolean;
  showDigitalRelease: boolean;
  showPhysicalRelease: boolean;
  showReleaseDate: boolean;
  showRuntime: boolean;
  showPath: boolean;
  showSizeOnDisk: boolean;
}

interface InfoRow {
  name: string;
  visible: boolean;
  title?: string;
  iconName: IconName;
  label: string;
}

const infoRowHeight = parseInt(dimensions.seriesIndexOverviewInfoRowHeight);

function LibraryIndexOverviewInfo(props: LibraryIndexOverviewInfoProps) {
  const { height, item, qualityProfile, sortKey } = props;

  const uiSettings = useUiSettingsValues();
  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    uiSettings;

  const relativeDateProps = {
    shortDateFormat,
    showRelativeDates,
    timeFormat,
    timeForToday: true,
  };

  const dateRow = (
    name: string,
    date: string | undefined,
    show: boolean,
    title: string,
    iconName: IconName
  ): InfoRow | null => {
    if (!date || (!show && sortKey !== name)) {
      return null;
    }

    return {
      name,
      visible: true,
      title: `${title}: ${formatDateTime(date, longDateFormat, timeFormat)}`,
      iconName,
      label: getRelativeDate({ date, ...relativeDateProps }) ?? '',
    };
  };

  const candidateRows: (InfoRow | null)[] = [];

  if (item.monitored || sortKey === 'monitored') {
    const monitoredText = item.monitored
      ? translate('Monitored')
      : translate('Unmonitored');

    candidateRows.push({
      name: 'monitored',
      visible: true,
      title: monitoredText,
      iconName: item.monitored ? icons.MONITORED : icons.UNMONITORED,
      label: monitoredText,
    });
  }

  if (item.type === 'series' && item.series) {
    const { network, previousAiring, added, statistics } = item.series;

    if (network && (props.showNetwork || sortKey === 'network')) {
      candidateRows.push({
        name: 'network',
        visible: true,
        title: translate('Network'),
        iconName: icons.NETWORK,
        label: network,
      });
    }

    candidateRows.push(
      dateRow(
        'previousAiring',
        previousAiring,
        props.showPreviousAiring,
        translate('PreviousAiring'),
        icons.CALENDAR
      )
    );

    candidateRows.push(
      dateRow('added', added, props.showAdded, translate('Added'), icons.ADD)
    );

    const seasonCount = statistics?.seasonCount ?? 0;

    if (props.showSeasonCount || sortKey === 'seasonCount') {
      let seasons = translate('OneSeason');

      if (seasonCount === 0) {
        seasons = translate('NoSeasons');
      } else if (seasonCount > 1) {
        seasons = translate('CountSeasons', { count: seasonCount });
      }

      candidateRows.push({
        name: 'seasonCount',
        visible: true,
        title: translate('SeasonCount'),
        iconName: icons.CIRCLE,
        label: seasons,
      });
    }
  }

  if (item.type === 'movie' && item.movie) {
    const {
      studio,
      collection,
      added,
      runtime,
      inCinemas,
      digitalRelease,
      physicalRelease,
      releaseDate,
    } = item.movie;

    if (studio && (props.showStudio || sortKey === 'studio')) {
      candidateRows.push({
        name: 'studio',
        visible: true,
        title: translate('Studio'),
        iconName: icons.MOVIES,
        label: studio,
      });
    }

    if (
      collection?.title &&
      (props.showCollection || sortKey === 'collection')
    ) {
      candidateRows.push({
        name: 'collection',
        visible: true,
        title: translate('Collection'),
        iconName: icons.MOVIES,
        label: collection.title,
      });
    }

    if (runtime && (props.showRuntime || sortKey === 'runtime')) {
      candidateRows.push({
        name: 'runtime',
        visible: true,
        title: translate('Runtime'),
        iconName: icons.HISTORY,
        label: `${Math.floor(runtime / 60)}h ${runtime % 60}m`,
      });
    }

    candidateRows.push(
      dateRow(
        'inCinemas',
        inCinemas,
        props.showCinemaRelease,
        translate('InCinemas'),
        icons.CALENDAR
      )
    );

    candidateRows.push(
      dateRow(
        'digitalRelease',
        digitalRelease,
        props.showDigitalRelease,
        translate('DigitalRelease'),
        icons.CALENDAR
      )
    );

    candidateRows.push(
      dateRow(
        'physicalRelease',
        physicalRelease,
        props.showPhysicalRelease,
        translate('PhysicalRelease'),
        icons.CALENDAR
      )
    );

    candidateRows.push(
      dateRow(
        'releaseDate',
        releaseDate,
        props.showReleaseDate,
        translate('ReleaseDate'),
        icons.CALENDAR
      )
    );

    candidateRows.push(
      dateRow('added', added, props.showAdded, translate('Added'), icons.ADD)
    );
  }

  if (
    qualityProfile?.name &&
    (props.showQualityProfile || sortKey === 'qualityProfileId')
  ) {
    candidateRows.push({
      name: 'qualityProfileId',
      visible: true,
      title: translate('QualityProfile'),
      iconName: icons.PROFILE,
      label: qualityProfile.name,
    });
  }

  const path = item.series?.path ?? item.movie?.path;

  if (path && (props.showPath || sortKey === 'path')) {
    candidateRows.push({
      name: 'path',
      visible: true,
      title: translate('Path'),
      iconName: icons.FOLDER,
      label: path,
    });
  }

  if (props.showSizeOnDisk || sortKey === 'sizeOnDisk') {
    candidateRows.push({
      name: 'sizeOnDisk',
      visible: true,
      title: translate('SizeOnDisk'),
      iconName: icons.DRIVE,
      label: formatBytes(item.sizeOnDisk ?? 0),
    });
  }

  const rows = candidateRows.filter((row): row is InfoRow => row != null);
  const maxRows = Math.floor(height / (infoRowHeight + 4));

  return (
    <div className="ml-0 flex shrink-0 basis-[250px] flex-col md:ml-[10px]">
      {rows.slice(0, Math.max(maxRows, 1)).map((row) => (
        <LibraryIndexOverviewInfoRow
          key={row.name}
          title={row.title}
          iconName={row.iconName}
          label={row.label}
        />
      ))}
    </div>
  );
}

export default LibraryIndexOverviewInfo;
