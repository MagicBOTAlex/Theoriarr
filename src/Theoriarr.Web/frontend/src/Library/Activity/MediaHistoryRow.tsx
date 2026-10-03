import React, { useCallback, useState } from 'react';
import HistoryDetailsModal from 'Activity/History/Details/HistoryDetailsModal';
import { getIconName, getTooltip } from 'Activity/History/HistoryEventTypeCell';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import { useEpisodesWithIds } from 'Episode/useEpisode';
import { icons, tooltipPositions } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import { MediaHistoryItem } from './mediaActivity';
import { useMarkMediaHistoryFailed } from './mediaActivityActions';
import { MediaStatusBadge, MediaTypeBadge } from './MediaActivityBadges';
import { getHistoryEventBadge } from './mediaActivityStatus';

interface MediaHistoryRowProps {
  item: MediaHistoryItem;
  columns: Column[];
}

function MediaHistoryRow({ item, columns }: MediaHistoryRowProps) {
  const series = useSingleSeries(item.seriesId);
  const episodes = useEpisodesWithIds(item.episodeId ? [item.episodeId] : []);
  const { markAsFailed } = useMarkMediaHistoryFailed(item.type, item.id);
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const handleMarkAsFailedPress = useCallback(() => {
    markAsFailed();
  }, [markAsFailed]);

  const handleDetailsPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  const isSeries = item.type === 'series';
  const eventType =
    item.eventType ?? item.historyEventType ?? item.movieEventType;
  const showMarkAsFailed = !isSeries && eventType === 'grabbed';
  const customFormats = item.customFormats ?? [];

  const eventBadge = getHistoryEventBadge(item);
  const eventIcon =
    isSeries && item.historyEventType && item.historyData
      ? getIconName(item.historyEventType, item.historyData)
      : undefined;
  const eventTooltip =
    isSeries && item.historyEventType && item.historyData
      ? getTooltip(item.historyEventType, item.historyData)
      : undefined;

  const canOpenDetails =
    isSeries && !!item.historyEventType && !!item.historyData;

  return (
    <TableRow>
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

        if (name === 'event') {
          return (
            <TableRowCell key={name}>
              {eventBadge ? (
                <span title={eventTooltip} className="inline-flex">
                  <MediaStatusBadge
                    label={eventBadge.label}
                    kind={eventBadge.kind}
                    icon={eventIcon}
                  />
                </span>
              ) : null}
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
              {item.quality ? (
                <EpisodeQuality
                  quality={item.quality}
                  isCutoffNotMet={item.qualityCutoffNotMet}
                />
              ) : null}
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

        if (name === 'date') {
          return <RelativeDateCell key={name} date={item.date} />;
        }

        if (name === 'downloadClient') {
          return (
            <TableRowCell key={name}>{item.downloadClient ?? ''}</TableRowCell>
          );
        }

        if (name === 'indexer') {
          return <TableRowCell key={name}>{item.indexer ?? ''}</TableRowCell>;
        }

        if (name === 'releaseGroup') {
          return (
            <TableRowCell key={name}>{item.releaseGroup ?? ''}</TableRowCell>
          );
        }

        if (name === 'sourceTitle') {
          return (
            <TableRowCell key={name}>
              <span className="block max-w-[360px] truncate">
                {item.sourceTitle}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'details') {
          if (canOpenDetails) {
            return (
              <TableRowCell key={name} className="whitespace-nowrap text-right">
                <IconButton
                  name={icons.INFO}
                  aria-label={translate('Details')}
                  onPress={handleDetailsPress}
                />
              </TableRowCell>
            );
          }

          if (showMarkAsFailed) {
            return (
              <TableRowCell key={name} className="whitespace-nowrap text-right">
                <IconButton
                  name={icons.REMOVE}
                  title={translate('MarkAsFailed')}
                  aria-label={translate('MarkAsFailed')}
                  onPress={handleMarkAsFailedPress}
                />
              </TableRowCell>
            );
          }

          return <TableRowCell key={name} className="whitespace-nowrap" />;
        }

        return null;
      })}

      {canOpenDetails ? (
        <HistoryDetailsModal
          id={item.id}
          isOpen={isDetailsModalOpen}
          eventType={item.historyEventType!}
          sourceTitle={item.sourceTitle}
          data={item.historyData!}
          downloadId={item.downloadId}
          onModalClose={handleDetailsModalClose}
        />
      ) : null}
    </TableRow>
  );
}

export default MediaHistoryRow;
