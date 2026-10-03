import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useState } from 'react';
import { useQueueItemForEpisode } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCalendarOptions } from 'Calendar/calendarOptionsStore';
import { CALENDAR_EVENT_STATUS_CLASSES } from 'Calendar/Events/CalendarEvent';
import CalendarEventQueueDetails from 'Calendar/Events/CalendarEventQueueDetails';
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
import formatTime from 'Utilities/Date/formatTime';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';

const EVENT_CLASS =
  'relative p-[5px] border-b border-solid border-b-[var(--borderColor)]';
const UNDERLAY_CLASS =
  'absolute top-0 left-0 block w-full h-full hover:bg-[var(--tableRowHoverBackgroundColor)]';
const OVERLAY_CLASS =
  'relative top-0 left-0 flex overflow-x-hidden w-full h-full text-[14px] pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto [&.colorImpaired]:border-l-[5px] max-[768px]:flex-col';
const EVENT_WRAPPER_CLASS =
  'flex flex-[1_0_1px] overflow-x-hidden pl-[6px] border-l-4 [border-left-style:solid] max-[768px]:block max-[768px]:flex-[0_0_auto]';
const DATE_CLASS =
  'flex-[0_0_250px] font-bold max-[768px]:ml-[10px] max-[768px]:flex-[0_0_100%]';
const TIME_CLASS =
  'flex-[0_0_125px] mr-[10px] border-none! max-[768px]:flex-[0_0_100%]';
const SERIES_TITLE_CLASS =
  'flex-[0_1_300px] overflow-hidden! mr-[10px] max-w-full text-ellipsis! whitespace-nowrap! max-[768px]:flex-[0_0_100%]';
const EPISODE_TITLE_CLASS =
  'flex-[1_1_1px] overflow-hidden! mr-[10px] max-w-full text-ellipsis! whitespace-nowrap!';
const SEASON_EPISODE_NUMBER_CLASS =
  'flex-[0_0_100px] max-[768px]:flex-[0_0_auto]';
const EPISODE_SEPARATOR_CLASS =
  'hidden max-[768px]:inline-block max-[768px]:m-[0_5px]';
const ABSOLUTE_EPISODE_NUMBER_CLASS = 'ml-[3px]';
const STATUS_ICON_CLASS = 'ml-[3px] cursor-default pointer-events-auto';

interface AgendaEventProps {
  id: number;
  seriesId: number;
  episodeFileId: number;
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
  showDate: boolean;
}

function AgendaEvent(props: AgendaEventProps) {
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
    showDate,
  } = props;

  const series = useSingleSeries(seriesId)!;
  const episodeFile = useEpisodeFile(episodeFileId);
  const queueItem = useQueueItemForEpisode(id);
  const { timeFormat, longDateFormat, enableColorImpairedMode } =
    useUiSettingsValues();

  const {
    showEpisodeInformation,
    showFinaleIcon,
    showSpecialIcon,
    showCutoffUnmetIcon,
  } = useCalendarOptions();

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const startTime = moment(airDateUtc);
  const endTime = moment(airDateUtc).add(series.runtime, 'minutes');
  const downloading = !!(queueItem || grabbed);
  const isMonitored = series.monitored && monitored;
  const statusStyle = getStatusStyle(
    hasFile,
    downloading,
    startTime,
    endTime,
    isMonitored
  );
  const missingAbsoluteNumber =
    series.seriesType === 'anime' && seasonNumber > 0 && !absoluteEpisodeNumber;

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  return (
    <div className={EVENT_CLASS}>
      <Link className={UNDERLAY_CLASS} onPress={handlePress} />

      <div className={OVERLAY_CLASS}>
        <div className={DATE_CLASS}>
          {showDate && startTime.format(longDateFormat)}
        </div>

        <div
          className={classNames(
            EVENT_WRAPPER_CLASS,
            CALENDAR_EVENT_STATUS_CLASSES[statusStyle],
            enableColorImpairedMode && 'colorImpaired'
          )}
        >
          <div className={TIME_CLASS}>
            {formatTime(airDateUtc, timeFormat)} -{' '}
            {formatTime(endTime.toISOString(), timeFormat, {
              includeMinuteZero: true,
            })}
          </div>

          <div className={SERIES_TITLE_CLASS}>{series.title}</div>

          {showEpisodeInformation ? (
            <div className={SEASON_EPISODE_NUMBER_CLASS}>
              {seasonNumber}x{padNumber(episodeNumber, 2)}
              {series.seriesType === 'anime' && absoluteEpisodeNumber && (
                <span className={ABSOLUTE_EPISODE_NUMBER_CLASS}>
                  ({absoluteEpisodeNumber})
                </span>
              )}
              <div className={EPISODE_SEPARATOR_CLASS}> - </div>
            </div>
          ) : null}

          <div className={EPISODE_TITLE_CLASS}>
            {showEpisodeInformation ? title : null}
          </div>

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
          episodeFile &&
          episodeFile.qualityCutoffNotMet ? (
            <Icon
              className={STATUS_ICON_CLASS}
              name={icons.EPISODE_FILE}
              kind={kinds.WARNING}
              title={translate('QualityCutoffNotMet')}
            />
          ) : null}

          {episodeNumber === 1 && seasonNumber > 0 && (
            <Icon
              className={STATUS_ICON_CLASS}
              name={icons.INFO}
              kind={kinds.INFO}
              title={
                seasonNumber === 1
                  ? translate('SeriesPremiere')
                  : translate('SeasonPremiere')
              }
            />
          )}

          {showFinaleIcon && finaleType ? (
            <Icon
              className={STATUS_ICON_CLASS}
              name={icons.INFO}
              kind={kinds.WARNING}
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

export default AgendaEvent;
