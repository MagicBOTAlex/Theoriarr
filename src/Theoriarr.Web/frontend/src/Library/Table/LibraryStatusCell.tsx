import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import StatusIndicator from 'Components/StatusIndicator';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import getMovieStatusDetails from 'Movies/getMovieStatusDetails';
import { MovieStatus } from 'Movies/Movie';
import { useToggleMovieMonitored } from 'Movies/useMovieMutations';
import { SeriesStatus } from 'Series/Series';
import { getSeriesStatusDetails } from 'Series/SeriesStatus';
import { useToggleSeriesMonitored } from 'Series/useSeries';
import translate from 'Utilities/String/translate';
import { MediaItem } from '../MediaItem';

const STATUS_ICON_CLASS = 'w-[20px]! text-center';

function getMonitoredLabel(item: MediaItem, isSeries: boolean) {
  if (isSeries) {
    return item.monitored
      ? translate('SeriesIsMonitored')
      : translate('SeriesIsUnmonitored');
  }

  return item.monitored
    ? translate('MovieIsMonitored')
    : translate('MovieIsUnmonitored');
}

interface LibraryStatusCellProps {
  className: string;
  item: MediaItem;
  isSelectMode: boolean;
  component?: React.ElementType;
}

function LibraryStatusCell({
  className,
  item,
  isSelectMode,
  component: Component = TableRowCell,
  ...otherProps
}: LibraryStatusCellProps) {
  const seriesMonitored = useToggleSeriesMonitored(item.id);
  const movieMonitored = useToggleMovieMonitored(item.id);

  const isSeries = item.type === 'series';

  const statusDetails = isSeries
    ? getSeriesStatusDetails(item.status as SeriesStatus)
    : getMovieStatusDetails(item.status as MovieStatus);

  const { isTogglingSeriesMonitored } = seriesMonitored;
  const { isTogglingMovieMonitored } = movieMonitored;

  const onMonitoredPress = useCallback(() => {
    if (isSeries) {
      seriesMonitored.toggleSeriesMonitored({ monitored: !item.monitored });
    } else {
      movieMonitored.toggleMovieMonitored({ monitored: !item.monitored });
    }
  }, [isSeries, item.monitored, seriesMonitored, movieMonitored]);

  const monitoredLabel = getMonitoredLabel(item, isSeries);

  return (
    <Component className={className} {...otherProps}>
      {isSelectMode ? (
        <MonitorToggleButton
          className={STATUS_ICON_CLASS}
          monitored={item.monitored}
          isSaving={
            isSeries ? isTogglingSeriesMonitored : isTogglingMovieMonitored
          }
          onPress={onMonitoredPress}
        />
      ) : (
        <StatusIndicator
          className={STATUS_ICON_CLASS}
          label={monitoredLabel}
          title={monitoredLabel}
        >
          <Icon name={item.monitored ? icons.MONITORED : icons.UNMONITORED} />
        </StatusIndicator>
      )}

      <StatusIndicator
        className={STATUS_ICON_CLASS}
        label={statusDetails.message}
        title={`${statusDetails.title}: ${statusDetails.message}`}
      >
        <Icon name={statusDetails.icon} />
      </StatusIndicator>
    </Component>
  );
}

export default LibraryStatusCell;
