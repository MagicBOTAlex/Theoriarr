import React, { useCallback, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import QueueStatus from 'Activity/Queue/QueueStatus';
import RemoveQueueItemModal from 'Activity/Queue/RemoveQueueItemModal';
import TimeLeftCell from 'Activity/Queue/TimeLeftCell';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import ProgressBar from 'Components/ProgressBar';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import { useEpisodesWithIds } from 'Episode/useEpisode';
import { icons, tooltipPositions } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import { useSingleSeries } from 'Series/useSeries';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { SelectStateInputProps } from 'typings/props';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import { MediaQueueItem } from './mediaActivity';
import {
  useFixMediaQueuePath,
  useGrabMediaQueueItem,
  useRedownloadMediaQueueItem,
  useRemoveMediaQueueItem,
} from './mediaActivityActions';
import { MediaStatusBadge, MediaTypeBadge } from './MediaActivityBadges';
import {
  getProgressBarKind,
  getQueueProgress,
  getQueueStatusBadge,
} from './mediaActivityStatus';

const PROGRESS_CONTAINER_CLASS =
  'relative h-[6px] w-full overflow-hidden rounded-full bg-[color-mix(in_srgb,var(--textColor)_12%,transparent)]! shadow-none';

const REPOSITORY_ISSUES_URL =
  'https://github.com/MagicBOTAlex/Theoriarr/issues/new';

interface MediaQueueRowProps {
  item: MediaQueueItem;
  columns: Column[];
  isSelected?: boolean;
  onSelectedChange?: (options: SelectStateInputProps<string>) => void;
  onModalOpenOrClose: (isOpen: boolean) => void;
}

function MediaQueueRow({
  item,
  columns,
  isSelected,
  onSelectedChange,
  onModalOpenOrClose,
}: MediaQueueRowProps) {
  const series = useSingleSeries(item.seriesId);
  const episodes = useEpisodesWithIds(item.episodeIds);
  const { showRelativeDates, shortDateFormat, timeFormat } =
    useUiSettingsValues();

  const { removeQueueItem, isRemoving } = useRemoveMediaQueueItem(
    item.type,
    item.id
  );
  const { grabQueueItem, isGrabbing } = useGrabMediaQueueItem(
    item.type,
    item.id
  );
  const { fixQueuePath, isFixingPath } = useFixMediaQueuePath(
    item.type,
    item.id
  );
  const { redownloadQueueItem, isRedownloading } = useRedownloadMediaQueueItem(
    item.type,
    item.id
  );

  const [isRemoveModalOpen, setIsRemoveModalOpen] = useState(false);
  const [isRedownloadModalOpen, setIsRedownloadModalOpen] = useState(false);
  const [isInteractiveImportModalOpen, setIsInteractiveImportModalOpen] =
    useState(false);

  const handleGrabPress = useCallback(() => {
    grabQueueItem();
  }, [grabQueueItem]);

  const handleInteractiveImportPress = useCallback(() => {
    onModalOpenOrClose(true);
    setIsInteractiveImportModalOpen(true);
  }, [onModalOpenOrClose]);

  const handleInteractiveImportModalClose = useCallback(() => {
    onModalOpenOrClose(false);
    setIsInteractiveImportModalOpen(false);
  }, [onModalOpenOrClose]);

  const handleRemovePress = useCallback(() => {
    onModalOpenOrClose(true);
    setIsRemoveModalOpen(true);
  }, [onModalOpenOrClose]);

  const handleFixPathPress = useCallback(() => {
    fixQueuePath();
  }, [fixQueuePath]);

  const handleReportPathIssuePress = useCallback(() => {
    window.open(REPOSITORY_ISSUES_URL, '_blank', 'noopener,noreferrer');
  }, []);

  const handleRedownloadPress = useCallback(() => {
    onModalOpenOrClose(true);
    setIsRedownloadModalOpen(true);
  }, [onModalOpenOrClose]);

  const handleRedownloadConfirmed = useCallback(() => {
    onModalOpenOrClose(false);
    redownloadQueueItem();
    setIsRedownloadModalOpen(false);
  }, [onModalOpenOrClose, redownloadQueueItem]);

  const handleRedownloadModalClose = useCallback(() => {
    onModalOpenOrClose(false);
    setIsRedownloadModalOpen(false);
  }, [onModalOpenOrClose]);

  const handleRemoveConfirmed = useCallback(() => {
    onModalOpenOrClose(false);
    removeQueueItem();
    setIsRemoveModalOpen(false);
  }, [onModalOpenOrClose, removeQueueItem]);

  const handleRemoveModalClose = useCallback(() => {
    onModalOpenOrClose(false);
    setIsRemoveModalOpen(false);
  }, [onModalOpenOrClose]);

  const isSeries = item.type === 'series';
  const trackedDownloadStatus = isSeries
    ? item.series?.trackedDownloadStatus
    : item.movie?.trackedDownloadStatus;
  const trackedDownloadState = isSeries
    ? item.series?.trackedDownloadState
    : item.movie?.trackedDownloadState;
  const statusMessages = isSeries
    ? item.series?.statusMessages
    : item.movie?.statusMessages;
  const errorMessage = isSeries
    ? item.series?.errorMessage
    : item.movie?.errorMessage;
  const downloadClientHasPostImportCategory = isSeries
    ? item.series?.downloadClientHasPostImportCategory
    : item.movie?.downloadClientHasPostImportCategory;

  const showInteractiveImport =
    isSeries &&
    item.status === 'completed' &&
    trackedDownloadStatus === 'warning';

  const canFixPath = !!item.suggestedOutputPath && !!item.pathCorrectionSupported;
  const pathFixUnavailable =
    !!item.pathNotAccessible && !item.pathCorrectionSupported;

  const statusBadge = getQueueStatusBadge(item);
  const progress = getQueueProgress(item);
  const progressKind = getProgressBarKind(statusBadge.kind);
  const customFormats = item.customFormats ?? [];
  const quality = item.series?.quality ?? item.movie?.quality;

  return (
    <TableRow>
      {onSelectedChange ? (
        <TableSelectCell
          id={item.key}
          isSelected={isSelected}
          onSelectedChange={onSelectedChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'type') {
          return (
            <TableRowCell key={name}>
              <MediaTypeBadge type={item.type} />
            </TableRowCell>
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name}>
              <div className="flex items-center gap-[6px]">
                <MediaStatusBadge
                  label={statusBadge.label}
                  kind={statusBadge.kind}
                />

                <QueueStatus
                  sourceTitle={item.title}
                  status={item.status}
                  trackedDownloadStatus={trackedDownloadStatus}
                  trackedDownloadState={trackedDownloadState}
                  statusMessages={statusMessages}
                  errorMessage={errorMessage}
                  position="right"
                />
              </div>
            </TableRowCell>
          );
        }

        if (name === 'media') {
          if (series) {
            return (
              <TableRowCell key={name}>
                <Link
                  className="block max-w-[220px] truncate"
                  to={`/series/${series.titleSlug}`}
                >
                  {series.title}
                </Link>
              </TableRowCell>
            );
          }

          if (item.movieLink) {
            return (
              <TableRowCell key={name}>
                <Link
                  className="block max-w-[220px] truncate"
                  to={item.movieLink}
                >
                  {item.movieTitle}
                </Link>
              </TableRowCell>
            );
          }

          return <TableRowCell key={name} />;
        }

        if (name === 'episode') {
          return (
            <TableRowCell key={name} className="whitespace-nowrap">
              {episodes
                .map(
                  (episode) =>
                    `S${String(episode.seasonNumber).padStart(2, '0')}E${String(
                      episode.episodeNumber
                    ).padStart(2, '0')}`
                )
                .join(', ')}
            </TableRowCell>
          );
        }

        if (name === 'episodeTitle') {
          return (
            <TableRowCell key={name}>
              <span className="block max-w-[300px] truncate">
                {episodes.map((episode) => episode.title).join(', ')}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'airDate') {
          return <RelativeDateCell key={name} date={episodes[0]?.airDateUtc} />;
        }

        if (name === 'title') {
          return (
            <TableRowCell key={name}>
              <span className="block max-w-[360px] truncate" title={item.title}>
                {item.title}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name}>
              <EpisodeLanguages languages={item.languages ?? []} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name}>
              {quality ? <EpisodeQuality quality={quality} /> : null}
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name}>
              <EpisodeFormats formats={customFormats} />
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name}>
              <Tooltip
                anchor={formatCustomFormatScore(
                  item.customFormatScore ?? 0,
                  customFormats.length
                )}
                tooltip={<EpisodeFormats formats={customFormats} />}
                position={tooltipPositions.BOTTOM}
              />
            </TableRowCell>
          );
        }

        if (name === 'protocol') {
          return (
            <TableRowCell key={name}>
              {item.protocol ? (
                <ProtocolLabel protocol={item.protocol} />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'indexer') {
          return <TableRowCell key={name}>{item.indexer}</TableRowCell>;
        }

        if (name === 'downloadClient') {
          return <TableRowCell key={name}>{item.downloadClient}</TableRowCell>;
        }

        if (name === 'size') {
          return (
            <TableRowCell key={name} className="whitespace-nowrap">
              {formatBytes(item.size)}
            </TableRowCell>
          );
        }

        if (name === 'outputPath') {
          return (
            <TableRowCell key={name}>
              {item.outputPath || item.suggestedOutputPath}
            </TableRowCell>
          );
        }

        if (name === 'timeLeft') {
          return (
            <TimeLeftCell
              key={name}
              status={item.status}
              estimatedCompletionTime={item.estimatedCompletionTime}
              timeLeft={item.timeLeft}
              size={item.size}
              sizeLeft={item.sizeLeft}
              showRelativeDates={showRelativeDates}
              shortDateFormat={shortDateFormat}
              timeFormat={timeFormat}
            />
          );
        }

        if (name === 'added') {
          return <RelativeDateCell key={name} date={item.added} />;
        }

        if (name === 'progress') {
          return (
            <TableRowCell key={name}>
              <div className="min-w-[110px]">
                <ProgressBar
                  progress={progress}
                  kind={progressKind}
                  containerClassName={PROGRESS_CONTAINER_CLASS}
                  ariaLabel={translate('ProgressBarProgress', {
                    progress: progress.toFixed(0),
                  })}
                />
              </div>
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className="whitespace-nowrap text-right">
              {showInteractiveImport ? (
                <IconButton
                  name={icons.INTERACTIVE}
                  aria-label={translate('InteractiveSearch')}
                  onPress={handleInteractiveImportPress}
                />
              ) : null}

              {canFixPath ? (
                <SpinnerIconButton
                  title={translate('FixDownloadPath')}
                  name={icons.EDIT}
                  isSpinning={isFixingPath}
                  onPress={handleFixPathPress}
                />
              ) : null}

              {pathFixUnavailable ? (
                <Tooltip
                  anchor={
                    <IconButton
                      name={icons.INFO}
                      aria-label={translate('FixDownloadPathUnavailable')}
                      onPress={handleReportPathIssuePress}
                    />
                  }
                  tooltip={translate('FixDownloadPathUnavailableTooltip', {
                    downloadClient: item.downloadClient ?? '',
                  })}
                  position={tooltipPositions.LEFT}
                />
              ) : null}

              {item.corruptFileDetected ? (
                <SpinnerIconButton
                  title={translate('DeleteAndRedownload')}
                  name={icons.REFRESH}
                  isSpinning={isRedownloading}
                  onPress={handleRedownloadPress}
                />
              ) : null}

              {item.isPending ? (
                <SpinnerIconButton
                  name={icons.DOWNLOAD}
                  aria-label={translate('Grab')}
                  isSpinning={isGrabbing}
                  onPress={handleGrabPress}
                />
              ) : null}

              <SpinnerIconButton
                title={translate('RemoveFromQueue')}
                name={icons.REMOVE}
                isSpinning={isRemoving}
                onPress={handleRemovePress}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      {item.downloadId ? (
        <InteractiveImportModal
          isOpen={isInteractiveImportModalOpen}
          downloadIds={[item.downloadId]}
          title={item.title}
          onModalClose={handleInteractiveImportModalClose}
        />
      ) : null}

      <RemoveQueueItemModal
        isOpen={isRemoveModalOpen}
        sourceTitle={item.title}
        canChangeCategory={!!downloadClientHasPostImportCategory}
        canIgnore={isSeries}
        isPending={item.isPending}
        downloadClient={item.downloadClient}
        onRemovePress={handleRemoveConfirmed}
        onModalClose={handleRemoveModalClose}
      />

      <ConfirmModal
        isOpen={isRedownloadModalOpen}
        kind="danger"
        title={translate('DeleteAndRedownload')}
        message={translate('DeleteAndRedownloadConfirmation')}
        confirmLabel={translate('DeleteAndRedownload')}
        isSpinning={isRedownloading}
        onConfirm={handleRedownloadConfirmed}
        onCancel={handleRedownloadModalClose}
      />
    </TableRow>
  );
}

export default MediaQueueRow;
