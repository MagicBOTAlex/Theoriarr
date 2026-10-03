import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useState } from 'react';
import { useQueueItemForEpisode } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import EpisodeDetailsModal from 'Episode/EpisodeDetailsModal';
import episodeEntities from 'Episode/episodeEntities';
import getFinaleTypeName from 'Episode/getFinaleTypeName';
import { useEpisodeFile } from 'EpisodeFile/EpisodeFileProvider';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { CalendarStatus } from 'typings/Calendar';
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import CalendarEventQueueDetails from './CalendarEventQueueDetails';

const EVENT_CLASS =
  'relative m-[4px_2px] p-[5px] border-b border-solid border-b-[var(--calendarBorderColor)] border-l-4 border-l-[var(--calendarBorderColor)]';
const UNDERLAY_CLASS = 'absolute top-0 left-0 block w-full h-full';
const OVERLAY_CLASS =
  'relative top-0 left-0 block overflow-x-hidden w-full h-full text-[12px] pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto [&.colorImpaired]:border-l-[5px]';
const INFO_CLASS = 'flex';
const EPISODE_INFO_CLASS = 'flex text-[var(--calendarTextDim)]';
const SERIES_TITLE_CLASS =
  'flex-[1_0_1px] overflow-hidden! mr-[10px] max-w-full text-ellipsis! whitespace-nowrap! text-[var(--calendarTextDimAlternate)] text-[14px]';
const EPISODE_TITLE_CLASS =
  'flex-[1_0_1px] overflow-hidden! mr-[10px] max-w-full text-ellipsis! whitespace-nowrap!';
const ABSOLUTE_EPISODE_NUMBER_CLASS = 'ml-[3px]';
const STATUS_CONTAINER_CLASS =
  'flex items-center [&.fullColor]:[filter:var(--calendarFullColorFilter)]';
const STATUS_ICON_CLASS = 'ml-[3px] cursor-default pointer-events-auto';
const AIR_TIME_CLASS = 'text-[var(--calendarTextDim)]';

export const CALENDAR_EVENT_STATUS_CLASSES: Record<CalendarStatus, string> = {
  downloaded:
    'border-l-[var(--successColor)]! [&.fullColor]:bg-[rgba(39,194,76,0.4)]! [&.colorImpaired]:border-l-[rgb(21,213,66)]!',
  downloading:
    'border-l-[var(--purple)]! [&.fullColor]:bg-[rgba(122,67,182,0.4)]!',
  unmonitored:
    'border-l-[var(--gray)]! [&.fullColor]:bg-[rgba(173,173,173,0.5)]! [&.colorImpaired]:[background:repeating-linear-gradient(45deg,var(--colorImpairedGradientDark),var(--colorImpairedGradientDark)_5px,var(--colorImpairedGradient)_5px,var(--colorImpairedGradient)_10px)] [&.fullColor.colorImpaired]:[background:repeating-linear-gradient(45deg,rgba(244,245,246,0.2),rgba(244,245,246,0.2)_5px,transparent_5px,transparent_10px)]',
  onAir:
    'border-l-[var(--warningColor)]! [&.fullColor]:bg-[rgba(255,165,0,0.6)]! [&.colorImpaired]:[background:repeating-linear-gradient(90deg,var(--colorImpairedGradientDark),var(--colorImpairedGradientDark)_5px,var(--colorImpairedGradient)_5px,var(--colorImpairedGradient)_10px)] [&.fullColor.colorImpaired]:[background:repeating-linear-gradient(90deg,rgba(244,245,246,0.2),rgba(244,245,246,0.2)_5px,transparent_5px,transparent_10px)]',
  missing:
    'border-l-[var(--dangerColor)]! [&.fullColor]:bg-[rgba(240,80,80,0.6)]! [&.colorImpaired]:border-l-[rgb(254,67,67)]! [&.colorImpaired]:[background:repeating-linear-gradient(90deg,var(--colorImpairedGradientDark),var(--colorImpairedGradientDark)_5px,var(--colorImpairedGradient)_5px,var(--colorImpairedGradient)_10px)] [&.fullColor.colorImpaired]:[background:repeating-linear-gradient(90deg,rgba(244,245,246,0.2),rgba(244,245,246,0.2)_5px,transparent_5px,transparent_10px)]',
  unaired:
    'border-l-[var(--primaryColor)]! [&.fullColor]:bg-[rgba(93,156,236,0.4)]! [&.colorImpaired]:[background:repeating-linear-gradient(90deg,var(--colorImpairedGradientDark),var(--colorImpairedGradientDark)_5px,var(--colorImpairedGradient)_5px,var(--colorImpairedGradient)_10px)] [&.fullColor.colorImpaired]:[background:repeating-linear-gradient(90deg,rgba(244,245,246,0.2),rgba(244,245,246,0.2)_5px,transparent_5px,transparent_10px)]',
};

interface CalendarEventProps {
  id: number;
  episodeId: number;
  seriesId: number;
  episodeFileId?: number;
  title: string;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  airDateUtc: string;
  monitored: boolean;
  unverifiedSceneNumbering?: boolean;
  finaleType?: string;
  hasFile: boolean;
  grabbed?: boolean;
  onEventModalOpenToggle: (isOpen: boolean) => void;
}

function CalendarEvent(props: CalendarEventProps) {
  const {
    id,
    seriesId,
    episodeFileId,
    title,
    seasonNumber,
    episodeNumber,
    absoluteEpisodeNumber,
    airDateUtc,
    monitored,
    unverifiedSceneNumbering,
    finaleType,
    hasFile,
    grabbed,
    onEventModalOpenToggle,
  } = props;

  const series = useSingleSeries(seriesId);
  const episodeFile = useEpisodeFile(episodeFileId);
  const queueItem = useQueueItemForEpisode(id);

  const { timeFormat, enableColorImpairedMode } = useUiSettingsValues();

  const {
    showEpisodeInformation,
    showFinaleIcon,
    showSpecialIcon,
    showCutoffUnmetIcon,
    fullColorEvents,
  } = useCalendarOptions();

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
    onEventModalOpenToggle(true);
  }, [onEventModalOpenToggle]);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
    onEventModalOpenToggle(false);
  }, [onEventModalOpenToggle]);

  if (!series) {
    return null;
  }

  const startTime = moment(airDateUtc);
  const endTime = moment(airDateUtc).add(series.runtime, 'minutes');
  const isDownloading = !!(queueItem || grabbed);
  const isMonitored = series.monitored && monitored;
  const statusStyle = getStatusStyle(
    hasFile,
    isDownloading,
    startTime,
    endTime,
    isMonitored
  );
  const missingAbsoluteNumber =
    series.seriesType === 'anime' && seasonNumber > 0 && !absoluteEpisodeNumber;

  return (
    <div
      className={classNames(
        EVENT_CLASS,
        CALENDAR_EVENT_STATUS_CLASSES[statusStyle],
        enableColorImpairedMode && 'colorImpaired',
        fullColorEvents && 'fullColor'
      )}
    >
      <Link className={UNDERLAY_CLASS} onPress={handlePress} />

      <div className={OVERLAY_CLASS}>
        <div className={INFO_CLASS}>
          <div className={SERIES_TITLE_CLASS}>{series.title}</div>

          <div
            className={classNames(
              STATUS_CONTAINER_CLASS,
              fullColorEvents && 'fullColor'
            )}
          >
            {missingAbsoluteNumber ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.WARNING}
                title={translate('EpisodeMissingAbsoluteNumber')}
              />
            ) : null}

            {unverifiedSceneNumbering && !missingAbsoluteNumber ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.WARNING}
                title={translate('SceneNumberNotVerified')}
              />
            ) : null}

            {queueItem ? (
              <span className={STATUS_ICON_CLASS}>
                <CalendarEventQueueDetails {...queueItem} />
              </span>
            ) : null}

            {!queueItem && grabbed ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.DOWNLOADING}
                title={translate('EpisodeIsDownloading')}
              />
            ) : null}

            {showCutoffUnmetIcon &&
            !!episodeFile &&
            episodeFile.qualityCutoffNotMet ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.EPISODE_FILE}
                kind={kinds.WARNING}
                title={translate('QualityCutoffNotMet')}
              />
            ) : null}

            {episodeNumber === 1 && seasonNumber > 0 ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.PREMIERE}
                kind={kinds.INFO}
                title={
                  seasonNumber === 1
                    ? translate('SeriesPremiere')
                    : translate('SeasonPremiere')
                }
              />
            ) : null}

            {showFinaleIcon && finaleType ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={
                  finaleType === 'series'
                    ? icons.FINALE_SERIES
                    : icons.FINALE_SEASON
                }
                kind={finaleType === 'series' ? kinds.DANGER : kinds.WARNING}
                title={getFinaleTypeName(finaleType)}
              />
            ) : null}

            {showSpecialIcon && (episodeNumber === 0 || seasonNumber === 0) ? (
              <Icon
                className={STATUS_ICON_CLASS}
                name={icons.INFO}
                kind={kinds.PINK}
                title={translate('Special')}
              />
            ) : null}
          </div>
        </div>

        {showEpisodeInformation ? (
          <div className={EPISODE_INFO_CLASS}>
            <div className={EPISODE_TITLE_CLASS}>{title}</div>

            <div>
              {seasonNumber}x{padNumber(episodeNumber, 2)}
              {series.seriesType === 'anime' && absoluteEpisodeNumber ? (
                <span className={ABSOLUTE_EPISODE_NUMBER_CLASS}>
                  ({absoluteEpisodeNumber})
                </span>
              ) : null}
            </div>
          </div>
        ) : null}

        <div className={AIR_TIME_CLASS}>
          {formatTime(airDateUtc, timeFormat)} -{' '}
          {formatTime(endTime.toISOString(), timeFormat, {
            includeMinuteZero: true,
          })}
        </div>
      </div>

      <EpisodeDetailsModal
        isOpen={isDetailsModalOpen}
        episodeId={id}
        episodeEntity={episodeEntities.CALENDAR}
        seriesId={series.id}
        episodeTitle={title}
        showOpenSeriesButton={true}
        onModalClose={handleDetailsModalClose}
      />
    </div>
  );
}

export default CalendarEvent;
