import React, { useCallback, useMemo, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import IndexerFlags from 'Episode/IndexerFlags';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import InteractiveSearchType from './InteractiveSearchType';
import MovieOverrideMatchModal from './OverrideMatch/MovieOverrideMatchModal';
import OverrideMatchModal from './OverrideMatch/OverrideMatchModal';
import Peers from './Peers';
import ReleaseSceneIndicator from './ReleaseSceneIndicator';
import { Release, useGrabRelease } from './useReleases';

const MANUAL_DOWNLOAD_CONTENT_CLASS =
  'relative inline-block mx-[2px] w-[22px] h-[20.39px] align-middle leading-[20.39px] hover:text-[var(--iconButtonHoverColor)]';

function getDownloadIcon(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return icons.SPINNER;
  } else if (isGrabbed) {
    return icons.DOWNLOADING;
  } else if (grabError) {
    return icons.DOWNLOADING;
  }

  return icons.DOWNLOAD;
}

function getDownloadKind(isGrabbed: boolean, grabError?: string) {
  if (isGrabbed) {
    return kinds.SUCCESS;
  }

  if (grabError) {
    return kinds.DANGER;
  }

  return kinds.DEFAULT;
}

function getDownloadTooltip(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return '';
  } else if (isGrabbed) {
    return translate('AddedToDownloadQueue');
  } else if (grabError) {
    return grabError;
  }

  return translate('AddToDownloadQueue');
}

interface InteractiveSearchRowProps extends Release {
  type: InteractiveSearchType;
  searchPayload: InteractiveSearchPayload;
}

function InteractiveSearchRow(props: InteractiveSearchRowProps) {
  const {
    decision,
    history,
    parsedInfo,
    release,
    languages,
    customFormatScore,
    customFormats,
    sceneMapping,
    mappedMovieId,
    mappedSeriesId,
    mappedSeasonNumber,
    mappedEpisodeNumbers,
    mappedAbsoluteEpisodeNumbers,
    mappedEpisodeInfo,
    episodeRequested,
    downloadAllowed,
    movieIndexerFlags,
    type,
    searchPayload,
  } = props;

  const isMovie = type === 'movie';

  const { rejections = [] } = decision;

  const {
    absoluteEpisodeNumbers,
    episodeNumbers,
    isDaily,
    seasonNumber,
    quality,
  } = parsedInfo;

  const {
    guid,
    indexerId,
    age,
    ageHours,
    ageMinutes,
    publishDate,
    title,
    infoUrl,
    indexer,
    size,
    seeders,
    leechers,
    protocol,
    indexerFlags = 0,
  } = release;

  const { longDateFormat, timeFormat } = useUiSettingsValues();

  const [isConfirmGrabModalOpen, setIsConfirmGrabModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const { isGrabbing, isGrabbed, grabError, grabRelease } = useGrabRelease(
    isMovie ? 'movies' : 'series'
  );

  const isBlocklisted = useMemo(() => {
    return (
      decision.rejections.findIndex((r) => r.reason === 'blocklisted') >= 0
    );
  }, [decision]);

  const handleGrabPress = useCallback(() => {
    if (downloadAllowed) {
      grabRelease({
        guid,
        indexerId,
      });

      return;
    }

    setIsConfirmGrabModalOpen(true);
  }, [
    guid,
    indexerId,
    downloadAllowed,
    grabRelease,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabConfirm = useCallback(() => {
    setIsConfirmGrabModalOpen(false);

    if (isMovie) {
      grabRelease({
        guid,
        indexerId,
        movieId: mappedMovieId,
      });

      return;
    }

    grabRelease({
      guid,
      indexerId,
      searchInfo: searchPayload,
    });
  }, [
    guid,
    indexerId,
    isMovie,
    mappedMovieId,
    searchPayload,
    grabRelease,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabCancel = useCallback(() => {
    setIsConfirmGrabModalOpen(false);
  }, [setIsConfirmGrabModalOpen]);

  const onOverridePress = useCallback(() => {
    setIsOverrideModalOpen(true);
  }, [setIsOverrideModalOpen]);

  const onOverrideModalClose = useCallback(() => {
    setIsOverrideModalOpen(false);
  }, [setIsOverrideModalOpen]);

  let indexerFlagsContent: React.ReactNode = null;

  if (isMovie) {
    if (movieIndexerFlags?.length) {
      indexerFlagsContent = (
        <Popover
          anchor={<Icon name={icons.FLAG} />}
          title={translate('IndexerFlags')}
          body={
            <ul>
              {movieIndexerFlags.map((flag, index) => {
                return <li key={index}>{flag}</li>;
              })}
            </ul>
          }
          position={tooltipPositions.LEFT}
        />
      );
    }
  } else if (indexerFlags) {
    indexerFlagsContent = (
      <Popover
        anchor={<Icon name={icons.FLAG} />}
        title={translate('IndexerFlags')}
        body={<IndexerFlags indexerFlags={indexerFlags} />}
        position={tooltipPositions.LEFT}
      />
    );
  }

  return (
    <TableRow>
      <TableRowCell className="w-[80px]">
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell
        className="whitespace-nowrap"
        title={formatDateTime(publishDate, longDateFormat, timeFormat, {
          includeSeconds: true,
        })}
      >
        {formatAge(age, ageHours, ageMinutes)}
      </TableRowCell>

      <TableRowCell className="w-full min-w-[280px]">
        <div className="flex items-center justify-between gap-2">
          <Link className="break-words" to={infoUrl}>
            {title}
          </Link>
          {isMovie ? null : (
            <ReleaseSceneIndicator
              className="shrink-0"
              seasonNumber={mappedSeasonNumber}
              episodeNumbers={mappedEpisodeNumbers}
              absoluteEpisodeNumbers={mappedAbsoluteEpisodeNumbers}
              sceneSeasonNumber={seasonNumber}
              sceneEpisodeNumbers={episodeNumbers}
              sceneAbsoluteEpisodeNumbers={absoluteEpisodeNumbers}
              sceneMapping={sceneMapping}
              episodeRequested={episodeRequested}
              isDaily={isDaily}
            />
          )}
        </div>
      </TableRowCell>

      <TableRowCell className="w-[85px]">{indexer}</TableRowCell>

      <TableRowCell className="w-[75px]">
        {history ? (
          <Icon
            name={icons.DOWNLOADING}
            kind={history.failed ? kinds.DANGER : kinds.DEFAULT}
            title={`${
              history.failed
                ? translate('FailedAt', {
                    date: formatDateTime(
                      history.failed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })
                : translate('GrabbedAt', {
                    date: formatDateTime(
                      history.grabbed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })
            }`}
          />
        ) : null}

        {isBlocklisted ? (
          <Icon
            containerClassName={history ? 'ml-[5px]' : undefined}
            name={icons.BLOCKLIST}
            kind={kinds.DANGER}
            title={
              history?.failed
                ? `${translate('BlocklistedAt', {
                    date: formatDateTime(
                      history.failed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })}`
                : translate('Blocklisted')
            }
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className="whitespace-nowrap">
        {formatBytes(size)}
      </TableRowCell>

      <TableRowCell className="w-[75px]">
        {protocol === 'torrent' ? (
          <Peers seeders={seeders} leechers={leechers} />
        ) : null}
      </TableRowCell>

      <TableRowCell className="text-center w-[100px]">
        <EpisodeLanguages languages={languages} />
      </TableRowCell>

      <TableRowCell className="text-center whitespace-nowrap">
        <EpisodeQuality quality={quality} showRevision={true} />
      </TableRowCell>

      <TableRowCell className="w-[55px] font-bold cursor-default">
        <Tooltip
          anchor={formatCustomFormatScore(
            customFormatScore,
            customFormats.length
          )}
          tooltip={<EpisodeFormats formats={customFormats} />}
          position={tooltipPositions.LEFT}
        />
      </TableRowCell>

      <TableRowCell className="w-[50px]">{indexerFlagsContent}</TableRowCell>

      <TableRowCell className="w-[50px]">
        {rejections.length ? (
          <Popover
            anchor={<Icon name={icons.DANGER} kind={kinds.DANGER} />}
            title={translate('ReleaseRejected')}
            body={
              <ul>
                {rejections.map((rejection, index) => {
                  return <li key={index}>{rejection.message}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className="w-[80px]">
        <SpinnerIconButton
          name={getDownloadIcon(isGrabbing, isGrabbed, grabError)}
          kind={getDownloadKind(isGrabbed, grabError)}
          title={getDownloadTooltip(isGrabbing, isGrabbed, grabError)}
          isSpinning={isGrabbing}
          onPress={handleGrabPress}
        />

        <Link
          className={MANUAL_DOWNLOAD_CONTENT_CLASS}
          title={translate('OverrideAndAddToDownloadQueue')}
          onPress={onOverridePress}
        >
          <div className={MANUAL_DOWNLOAD_CONTENT_CLASS}>
            <Icon
              className="absolute top-[4px] left-0 text-center"
              name={icons.INTERACTIVE}
              size={12}
            />

            <Icon
              className="absolute top-[7px] left-[8px] text-center"
              name={icons.CIRCLE_DOWN}
              size={10}
            />
          </div>
        </Link>
      </TableRowCell>

      <ConfirmModal
        isOpen={isConfirmGrabModalOpen}
        kind={kinds.WARNING}
        title={translate('GrabRelease')}
        message={translate(
          isMovie
            ? 'GrabReleaseMessageText'
            : 'GrabReleaseUnknownSeriesOrEpisodeMessageText',
          {
            title,
          }
        )}
        confirmLabel={translate('Grab')}
        onConfirm={onGrabConfirm}
        onCancel={onGrabCancel}
      />

      {isMovie ? (
        <MovieOverrideMatchModal
          isOpen={isOverrideModalOpen}
          title={title}
          indexerId={indexerId}
          guid={guid}
          movieId={mappedMovieId}
          languages={languages}
          quality={quality}
          protocol={protocol}
          isGrabbing={isGrabbing}
          grabError={grabError}
          grabRelease={grabRelease}
          onModalClose={onOverrideModalClose}
        />
      ) : (
        <OverrideMatchModal
          isOpen={isOverrideModalOpen}
          title={title}
          indexerId={indexerId}
          guid={guid}
          seriesId={mappedSeriesId}
          seasonNumber={mappedSeasonNumber}
          episodes={mappedEpisodeInfo}
          languages={languages}
          quality={quality}
          protocol={protocol}
          isGrabbing={isGrabbing}
          grabError={grabError}
          grabRelease={grabRelease}
          onModalClose={onOverrideModalClose}
        />
      )}
    </TableRow>
  );
}

export default InteractiveSearchRow;
