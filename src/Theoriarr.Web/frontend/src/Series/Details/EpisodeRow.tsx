import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeNumber from 'Episode/EpisodeNumber';
import EpisodeSearchCell from 'Episode/EpisodeSearchCell';
import EpisodeStatus from 'Episode/EpisodeStatus';
import EpisodeTitleLink from 'Episode/EpisodeTitleLink';
import IndexerFlags from 'Episode/IndexerFlags';
import EpisodeFileLanguages from 'EpisodeFile/EpisodeFileLanguages';
import { useEpisodeFile } from 'EpisodeFile/EpisodeFileProvider';
import MediaInfo from 'EpisodeFile/MediaInfo';
import { icons } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import MediaInfoModel from 'typings/MediaInfo';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import formatRuntime from 'Utilities/Number/formatRuntime';
import translate from 'Utilities/String/translate';

interface EpisodeRowProps {
  id: number;
  seriesId: number;
  episodeFileId?: number;
  monitored: boolean;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  sceneSeasonNumber?: number;
  sceneEpisodeNumber?: number;
  sceneAbsoluteEpisodeNumber?: number;
  airDateUtc?: string;
  runtime?: number;
  finaleType?: string;
  title: string;
  isSaving?: boolean;
  unverifiedSceneNumbering?: boolean;
  // episodeFilePath?: string;
  // episodeFileRelativePath?: string;
  // episodeFileSize?: number;
  // releaseGroup?: string;
  // customFormats?: CustomFormat[];
  // customFormatScore: number;
  // indexerFlags?: number;
  mediaInfo?: MediaInfoModel;
  columns: Column[];
  onMonitorEpisodePress: (
    episodeId: number,
    value: boolean,
    { shiftKey }: { shiftKey: boolean }
  ) => void;
}

function EpisodeRow({
  id,
  seriesId,
  episodeFileId,
  monitored,
  seasonNumber,
  episodeNumber,
  absoluteEpisodeNumber,
  sceneSeasonNumber,
  sceneEpisodeNumber,
  sceneAbsoluteEpisodeNumber,
  airDateUtc,
  runtime,
  finaleType,
  title,
  unverifiedSceneNumbering,
  isSaving,
  // episodeFilePath,
  // episodeFileRelativePath,
  // episodeFileSize,
  // releaseGroup,
  // customFormats = [],
  // customFormatScore,
  // indexerFlags = 0,
  columns,
  onMonitorEpisodePress,
}: EpisodeRowProps) {
  const {
    useSceneNumbering,
    monitored: seriesMonitored,
    seriesType,
    alternateTitles = [],
  } = useSingleSeries(seriesId)!;
  const episodeFile = useEpisodeFile(episodeFileId);

  const customFormats = episodeFile?.customFormats ?? [];
  const customFormatScore = episodeFile?.customFormatScore ?? 0;

  const handleMonitorEpisodePress = useCallback(
    (monitored: boolean, options: { shiftKey: boolean }) => {
      onMonitorEpisodePress(id, monitored, options);
    },
    [id, onMonitorEpisodePress]
  );

  return (
    <TableRow>
      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'monitored') {
          return (
            <TableRowCell key={name} className="w-[42px]">
              <MonitorToggleButton
                monitored={monitored}
                isDisabled={!seriesMonitored}
                isSaving={isSaving}
                onPress={handleMonitorEpisodePress}
              />
            </TableRowCell>
          );
        }

        if (name === 'episodeNumber') {
          return (
            <TableRowCell
              key={name}
              className={seriesType === 'anime' ? 'w-[95px]' : 'w-[65px]'}
            >
              <EpisodeNumber
                seasonNumber={seasonNumber}
                episodeNumber={episodeNumber}
                absoluteEpisodeNumber={absoluteEpisodeNumber}
                useSceneNumbering={useSceneNumbering}
                unverifiedSceneNumbering={unverifiedSceneNumbering}
                seriesType={seriesType}
                sceneSeasonNumber={sceneSeasonNumber}
                sceneEpisodeNumber={sceneEpisodeNumber}
                sceneAbsoluteEpisodeNumber={sceneAbsoluteEpisodeNumber}
                alternateTitles={alternateTitles}
              />
            </TableRowCell>
          );
        }

        if (name === 'title') {
          return (
            <TableRowCell key={name} className="whitespace-nowrap">
              <EpisodeTitleLink
                episodeId={id}
                seriesId={seriesId}
                episodeTitle={title}
                episodeEntity="episodes"
                finaleType={finaleType}
                showOpenSeriesButton={false}
              />
            </TableRowCell>
          );
        }

        if (name === 'path') {
          return <TableRowCell key={name}>{episodeFile?.path}</TableRowCell>;
        }

        if (name === 'relativePath') {
          return (
            <TableRowCell key={name}>{episodeFile?.relativePath}</TableRowCell>
          );
        }

        if (name === 'airDateUtc') {
          return <RelativeDateCell key={name} date={airDateUtc} />;
        }

        if (name === 'runtime') {
          return (
            <TableRowCell key={name} className="w-[80px]">
              {formatRuntime(runtime)}
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
            <TableRowCell key={name} className="w-[55px]">
              <Tooltip
                anchor={formatCustomFormatScore(
                  customFormatScore,
                  customFormats.length
                )}
                tooltip={<EpisodeFormats formats={customFormats} />}
                position="left"
              />
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name} className="w-[100px]">
              <EpisodeFileLanguages episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'audioInfo') {
          return (
            <TableRowCell key={name} className="w-[100px]">
              <MediaInfo type="audio" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'audioLanguages') {
          return (
            <TableRowCell key={name} className="w-[165px]">
              <MediaInfo type="audioLanguages" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'subtitleLanguages') {
          return (
            <TableRowCell key={name} className="w-[165px]">
              <MediaInfo type="subtitles" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'videoCodec') {
          return (
            <TableRowCell key={name} className="w-[100px]">
              <MediaInfo type="video" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'videoDynamicRangeType') {
          return (
            <TableRowCell key={name} className="w-[165px]">
              <MediaInfo
                type="videoDynamicRangeType"
                episodeFileId={episodeFileId}
              />
            </TableRowCell>
          );
        }

        if (name === 'size') {
          return (
            <TableRowCell key={name} className="w-[100px]">
              {!!episodeFile?.size && formatBytes(episodeFile?.size)}
            </TableRowCell>
          );
        }

        if (name === 'releaseGroup') {
          return (
            <TableRowCell key={name} className="w-[120px]">
              {episodeFile?.releaseGroup}
            </TableRowCell>
          );
        }

        if (name === 'indexerFlags') {
          return (
            <TableRowCell key={name} className="w-[50px]">
              {episodeFile?.indexerFlags ? (
                <Popover
                  anchor={<Icon name={icons.FLAG} kind="default" />}
                  title={translate('IndexerFlags')}
                  body={
                    <IndexerFlags indexerFlags={episodeFile?.indexerFlags} />
                  }
                  position="left"
                />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name} className="w-[100px]">
              <EpisodeStatus episodeId={id} episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <EpisodeSearchCell
              key={name}
              episodeId={id}
              episodeEntity="episodes"
              seriesId={seriesId}
              episodeTitle={title}
              showOpenSeriesButton={false}
            />
          );
        }

        return null;
      })}
    </TableRow>
  );
}

export default EpisodeRow;
