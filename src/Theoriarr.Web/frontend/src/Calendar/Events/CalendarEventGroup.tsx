import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useMemo, useState } from 'react';
import { useIsDownloadingEpisodes } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import getFinaleTypeName from 'Episode/getFinaleTypeName';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { CalendarItem } from 'typings/Calendar';
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import CalendarEvent, { CALENDAR_EVENT_STATUS_CLASSES } from './CalendarEvent';

const EVENT_GROUP_CLASS =
  'overflow-x-hidden m-[4px_2px] p-[5px] border-b border-solid border-b-[var(--borderColor)] border-l-4 border-l-[var(--borderColor)] text-[12px]';
const INFO_CLASS = 'flex';
const AIRING_INFO_CLASS = 'flex';
const SERIES_TITLE_CLASS =
  'flex-[1_0_1px] overflow-hidden! mr-[10px] max-w-full text-[var(--calendarTextDimAlternate)] text-ellipsis! whitespace-nowrap! text-[14px]';
const AIR_TIME_CLASS = 'flex-[1_0_1px] text-[var(--calendarTextDim)]';
const EPISODE_INFO_CLASS = 'ml-[10px] text-[var(--calendarTextDim)]';
const ABSOLUTE_EPISODE_NUMBER_CLASS = 'ml-[3px]';
const EXPAND_CONTAINER_INLINE_CLASS = 'flex justify-end flex-[1_0_20px]';
const EXPAND_CONTAINER_CLASS = 'flex items-center justify-center';
const COLLAPSE_CONTAINER_CLASS = 'flex items-center justify-center mb-[5px]';
const STATUS_CONTAINER_CLASS =
  'flex items-center [&.fullColor]:[filter:var(--calendarFullColorFilter)]';
const STATUS_ICON_CLASS = 'ml-[3px]';

interface CalendarEventGroupProps {
  episodeIds: number[];
  seriesId: number;
  events: CalendarItem[];
  onEventModalOpenToggle: (isOpen: boolean) => void;
}

function CalendarEventGroup({
  episodeIds,
  seriesId,
  events,
  onEventModalOpenToggle,
}: CalendarEventGroupProps) {
  const isDownloading = useIsDownloadingEpisodes(episodeIds);
  const series = useSingleSeries(seriesId)!;

  const { timeFormat, enableColorImpairedMode } = useUiSettingsValues();

  const { showEpisodeInformation, showFinaleIcon, fullColorEvents } =
    useCalendarOptions();

  const [isExpanded, setIsExpanded] = useState(false);

  const firstEpisode = events[0];
  const lastEpisode = events[events.length - 1];
  const airDateUtc = firstEpisode.airDateUtc;
  const startTime = moment(airDateUtc);
  const endTime = moment(lastEpisode.airDateUtc).add(series.runtime, 'minutes');
  const seasonNumber = firstEpisode.seasonNumber;

  const { allDownloaded, anyGrabbed, anyMonitored, allAbsoluteEpisodeNumbers } =
    useMemo(() => {
      let files = 0;
      let grabbed = 0;
      let monitored = 0;
      let absoluteEpisodeNumbers = 0;

      events.forEach((event) => {
        if (event.episodeFileId) {
          files++;
        }

        if (event.grabbed) {
          grabbed++;
        }

        if (series.monitored && event.monitored) {
          monitored++;
        }

        if (event.absoluteEpisodeNumber) {
          absoluteEpisodeNumbers++;
        }
      });

      return {
        allDownloaded: files === events.length,
        anyGrabbed: grabbed > 0,
        anyMonitored: monitored > 0,
        allAbsoluteEpisodeNumbers: absoluteEpisodeNumbers === events.length,
      };
    }, [series, events]);

  const anyDownloading = isDownloading || anyGrabbed;

  const statusStyle = getStatusStyle(
    allDownloaded,
    anyDownloading,
    startTime,
    endTime,
    anyMonitored
  );
  const isMissingAbsoluteNumber =
    series.seriesType === 'anime' &&
    seasonNumber > 0 &&
    !allAbsoluteEpisodeNumbers;

  const handleExpandPress = useCallback(() => {
    setIsExpanded((state) => !state);
  }, []);

  if (isExpanded) {
    return (
      <div>
        {events.map((event) => {
          return (
            <CalendarEvent
              key={event.id}
              episodeId={event.id}
              {...event}
              onEventModalOpenToggle={onEventModalOpenToggle}
            />
          );
        })}

        <Link
          className={COLLAPSE_CONTAINER_CLASS}
          component="div"
          onPress={handleExpandPress}
        >
          <Icon name={icons.COLLAPSE} />
        </Link>
      </div>
    );
  }

  return (
    <div
      className={classNames(
        EVENT_GROUP_CLASS,
        CALENDAR_EVENT_STATUS_CLASSES[statusStyle],
        enableColorImpairedMode && 'colorImpaired',
        fullColorEvents && 'fullColor'
      )}
    >
      <div className={INFO_CLASS}>
        <div className={SERIES_TITLE_CLASS}>{series.title}</div>

        <div
          className={classNames(
            STATUS_CONTAINER_CLASS,
            fullColorEvents && 'fullColor'
          )}
        >
          {isMissingAbsoluteNumber ? (
            <Icon
              containerClassName={STATUS_ICON_CLASS}
              name={icons.WARNING}
              title={translate('EpisodeMissingAbsoluteNumber')}
            />
          ) : null}

          {anyDownloading ? (
            <Icon
              containerClassName={STATUS_ICON_CLASS}
              name={icons.DOWNLOADING}
              title={translate('AnEpisodeIsDownloading')}
            />
          ) : null}

          {firstEpisode.episodeNumber === 1 && seasonNumber > 0 ? (
            <Icon
              containerClassName={STATUS_ICON_CLASS}
              name={icons.PREMIERE}
              kind={kinds.INFO}
              title={
                seasonNumber === 1
                  ? translate('SeriesPremiere')
                  : translate('SeasonPremiere')
              }
            />
          ) : null}

          {showFinaleIcon && lastEpisode.finaleType ? (
            <Icon
              containerClassName={STATUS_ICON_CLASS}
              name={
                lastEpisode.finaleType === 'series'
                  ? icons.FINALE_SERIES
                  : icons.FINALE_SEASON
              }
              kind={
                lastEpisode.finaleType === 'series'
                  ? kinds.DANGER
                  : kinds.WARNING
              }
              title={getFinaleTypeName(lastEpisode.finaleType)}
            />
          ) : null}
        </div>
      </div>

      <div className={AIRING_INFO_CLASS}>
        <div className={AIR_TIME_CLASS}>
          {formatTime(airDateUtc, timeFormat)} -{' '}
          {formatTime(endTime.toISOString(), timeFormat, {
            includeMinuteZero: true,
          })}
        </div>

        {showEpisodeInformation ? (
          <div className={EPISODE_INFO_CLASS}>
            {seasonNumber}x{padNumber(firstEpisode.episodeNumber, 2)}-
            {padNumber(lastEpisode.episodeNumber, 2)}
            {series.seriesType === 'anime' &&
            firstEpisode.absoluteEpisodeNumber &&
            lastEpisode.absoluteEpisodeNumber ? (
              <span className={ABSOLUTE_EPISODE_NUMBER_CLASS}>
                ({firstEpisode.absoluteEpisodeNumber}-
                {lastEpisode.absoluteEpisodeNumber})
              </span>
            ) : null}
          </div>
        ) : (
          <Link
            className={EXPAND_CONTAINER_INLINE_CLASS}
            component="div"
            onPress={handleExpandPress}
          >
            <Icon name={icons.EXPAND} />
          </Link>
        )}
      </div>

      {showEpisodeInformation ? (
        <Link
          className={EXPAND_CONTAINER_CLASS}
          component="div"
          onPress={handleExpandPress}
        >
          &nbsp;
          <Icon name={icons.EXPAND} />
          &nbsp;
        </Link>
      ) : null}
    </div>
  );
}

export default CalendarEventGroup;
