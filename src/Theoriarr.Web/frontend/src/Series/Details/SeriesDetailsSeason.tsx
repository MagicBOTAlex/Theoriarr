import classNames from 'classnames';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useAppDimension } from 'App/appStore';
import CommandNames from 'Commands/CommandNames';
import { useCommands, useExecuteCommand } from 'Commands/useCommands';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent, { MENU_CONTENT_CLASS } from 'Components/Menu/MenuContent';
import MenuItem from 'Components/Menu/MenuItem';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import SpinnerIcon from 'Components/SpinnerIcon';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Popover from 'Components/Tooltip/Popover';
import Episode from 'Episode/Episode';
import {
  setEpisodeOptions,
  setEpisodeSort,
  useEpisodeOptions,
} from 'Episode/episodeOptionsStore';
import { getQueryKey, useToggleEpisodesMonitored } from 'Episode/useEpisode';
import { useSeasonEpisodes } from 'Episode/useEpisodes';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { align, icons, sortDirections, tooltipPositions } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import CompressModal from 'Library/Compression/CompressModal';
import OrganizePreviewModal from 'Organize/OrganizePreviewModal';
import SeriesHistoryModal from 'Series/History/SeriesHistoryModal';
import SeasonInteractiveSearchModal from 'Series/Search/SeasonInteractiveSearchModal';
import { Statistics } from 'Series/Series';
import { useSingleSeries, useToggleSeasonMonitored } from 'Series/useSeries';
import { TableOptionsChangePayload } from 'typings/Table';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import isAfter from 'Utilities/Date/isAfter';
import isBefore from 'Utilities/Date/isBefore';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import getToggledRange from 'Utilities/Table/getToggledRange';
import EpisodeRow from './EpisodeRow';
import SeasonInfo from './SeasonInfo';
import SeasonProgressLabel from './SeasonProgressLabel';

const SEASON_CLASS =
  'mb-[20px] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardBackgroundColor)] [&:last-of-type]:mb-0 max-[768px]:border-x-0 max-[768px]:rounded-none';
const ACTIONS_CLASS = 'flex items-center flex-[0_1_350px] py-[15px] px-[10px]';

function getSeasonStatistics(episodes: Episode[]) {
  let episodeCount = 0;
  let episodeFileCount = 0;
  let totalEpisodeCount = 0;
  let monitoredEpisodeCount = 0;
  let hasMonitoredEpisodes = false;
  const sizeOnDisk = 0;

  episodes.forEach((episode) => {
    if (
      episode.episodeFileId ||
      (episode.monitored && isBefore(episode.airDateUtc))
    ) {
      episodeCount++;
    }

    if (episode.episodeFileId) {
      episodeFileCount++;
    }

    if (episode.monitored) {
      monitoredEpisodeCount++;
      hasMonitoredEpisodes = true;
    }

    totalEpisodeCount++;
  });

  return {
    episodeCount,
    episodeFileCount,
    totalEpisodeCount,
    monitoredEpisodeCount,
    hasMonitoredEpisodes,
    sizeOnDisk,
  };
}

function useIsSearching(seriesId: number, seasonNumber: number) {
  const { data: commands } = useCommands();
  return isCommandExecuting(
    findCommand(commands, {
      name: CommandNames.SeasonSearch,
      seriesId,
      seasonNumber,
    })
  );
}

interface SeriesDetailsSeasonProps {
  seriesId: number;
  monitored: boolean;
  seasonNumber: number;
  statistics?: Statistics;
  isExpanded?: boolean;
  onExpandPress: (seasonNumber: number, isExpanded: boolean) => void;
}

function SeriesDetailsSeason({
  seriesId,
  monitored,
  seasonNumber,
  statistics = {} as Statistics,
  isExpanded,
  onExpandPress,
}: SeriesDetailsSeasonProps) {
  const executeCommand = useExecuteCommand();
  const { monitored: seriesMonitored, path } = useSingleSeries(seriesId)!;
  const { data: items } = useSeasonEpisodes(seriesId, seasonNumber);

  const { columns, sortKey, sortDirection } = useEpisodeOptions();

  const isSmallScreen = useAppDimension('isSmallScreen');
  const isSearching = useIsSearching(seriesId, seasonNumber);

  const { sizeOnDisk = 0 } = statistics;

  const {
    episodeCount,
    episodeFileCount,
    totalEpisodeCount,
    monitoredEpisodeCount,
    hasMonitoredEpisodes,
  } = getSeasonStatistics(items);

  const previousEpisodeFileCount = usePrevious(episodeFileCount);

  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isManageEpisodesOpen, setIsManageEpisodesOpen] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isInteractiveSearchModalOpen, setIsInteractiveSearchModalOpen] =
    useState(false);
  const [isCompressModalOpen, setIsCompressModalOpen] = useState(false);

  const { toggleEpisodesMonitored, isToggling, togglingEpisodeIds } =
    useToggleEpisodesMonitored(getQueryKey('episodes')!);

  const { toggleSeasonMonitored, isTogglingSeasonMonitored } =
    useToggleSeasonMonitored(seriesId);

  const lastToggledEpisode = useRef<number | null>(null);
  const hasSetInitalExpand = useRef(false);

  const seasonNumberTitle =
    seasonNumber === 0
      ? translate('Specials')
      : translate('SeasonNumberToken', { seasonNumber });

  const handleMonitorSeasonPress = useCallback(
    (value: boolean) => {
      toggleSeasonMonitored({
        seasonNumber,
        monitored: value,
      });
    },
    [seasonNumber, toggleSeasonMonitored]
  );

  const handleExpandPress = useCallback(() => {
    onExpandPress(seasonNumber, !isExpanded);
  }, [seasonNumber, isExpanded, onExpandPress]);

  const handleMonitorEpisodePress = useCallback(
    (
      episodeId: number,
      value: boolean,
      { shiftKey }: { shiftKey: boolean }
    ) => {
      const lastToggled = lastToggledEpisode.current;
      const episodeIds = new Set([episodeId]);

      if (shiftKey && lastToggled) {
        const { lower, upper } = getToggledRange(items, episodeId, lastToggled);

        for (let i = lower; i < upper; i++) {
          episodeIds.add(items[i].id);
        }
      }

      lastToggledEpisode.current = episodeId;

      toggleEpisodesMonitored({
        episodeIds: Array.from(episodeIds),
        monitored: value,
      });
    },
    [items, toggleEpisodesMonitored]
  );

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeasonSearch,
      seriesId,
      seasonNumber,
    });
  }, [seriesId, seasonNumber, executeCommand]);

  const handleOrganizePress = useCallback(() => {
    setIsOrganizeModalOpen(true);
  }, []);

  const handleOrganizeModalClose = useCallback(() => {
    setIsOrganizeModalOpen(false);
  }, []);

  const handleManageEpisodesPress = useCallback(() => {
    setIsManageEpisodesOpen(true);
  }, []);

  const handleManageEpisodesModalClose = useCallback(() => {
    setIsManageEpisodesOpen(false);
  }, []);

  const handleHistoryPress = useCallback(() => {
    setIsHistoryModalOpen(true);
  }, []);

  const handleHistoryModalClose = useCallback(() => {
    setIsHistoryModalOpen(false);
  }, []);

  const handleInteractiveSearchPress = useCallback(() => {
    setIsInteractiveSearchModalOpen(true);
  }, []);

  const handleInteractiveSearchModalClose = useCallback(() => {
    setIsInteractiveSearchModalOpen(false);
  }, []);

  const handleCompressPress = useCallback(() => {
    setIsCompressModalOpen(true);
  }, []);

  const handleCompressModalClose = useCallback(() => {
    setIsCompressModalOpen(false);
  }, []);

  const handleSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      setEpisodeSort({
        sortKey,
        sortDirection,
      });
    },
    []
  );

  const handleTableOptionChange = useCallback(
    (payload: TableOptionsChangePayload) => {
      setEpisodeOptions(payload);
    },
    []
  );

  useEffect(() => {
    if (hasSetInitalExpand.current || items.length === 0) {
      return;
    }

    hasSetInitalExpand.current = true;

    const expand =
      items.some(
        (item) =>
          isAfter(item.airDateUtc) || isAfter(item.airDateUtc, { days: -30 })
      ) || items.every((item) => !item.airDateUtc);

    onExpandPress(seasonNumber, expand && seasonNumber > 0);
  }, [items, seriesId, seasonNumber, onExpandPress]);

  useEffect(() => {
    if ((previousEpisodeFileCount ?? 0) > 0 && episodeFileCount === 0) {
      setIsOrganizeModalOpen(false);
      setIsManageEpisodesOpen(false);
    }
  }, [episodeFileCount, previousEpisodeFileCount]);

  return (
    <div className={SEASON_CLASS}>
      <div className="relative flex items-center w-full text-[24px]">
        <div className="flex items-center flex-[0_1_350px] py-[15px] px-[10px]">
          <MonitorToggleButton
            monitored={monitored}
            isDisabled={!seriesMonitored}
            isSaving={isTogglingSeasonMonitored}
            size={24}
            onPress={handleMonitorSeasonPress}
          />

          <div className="mr-[10px] ml-[5px]">
            <div className="leading-[24px]">{seasonNumberTitle}</div>
          </div>

          <div className="flex items-end flex-col ml-auto gap-[2px]">
            <Popover
              className="flex items-stretch w-full"
              canFlip={true}
              anchor={
                <SeasonProgressLabel
                  className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! w-full"
                  seriesId={seriesId}
                  seasonNumber={seasonNumber}
                  monitored={monitored}
                  episodeCount={episodeCount}
                  episodeFileCount={episodeFileCount}
                />
              }
              title={translate('SeasonInformation')}
              body={
                <div>
                  <SeasonInfo
                    totalEpisodeCount={totalEpisodeCount}
                    monitoredEpisodeCount={monitoredEpisodeCount}
                    episodeFileCount={episodeFileCount}
                    sizeOnDisk={sizeOnDisk}
                  />
                </div>
              }
              position={tooltipPositions.BOTTOM}
            />

            {sizeOnDisk ? (
              <Label
                className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! w-full"
                kind="default"
                size="large"
              >
                {formatBytes(sizeOnDisk)}
              </Label>
            ) : null}
          </div>
        </div>

        <Link className="grow mx-5 text-center" onPress={handleExpandPress}>
          <Icon
            className="inline-block absolute top-1/2 left-1/2 -mt-3 -ml-3 w-[30px] mx-0.5 rounded-[4px] text-center [font-size:inherit] hover:text-[var(--iconButtonHoverColor)] max-[768px]:static max-[768px]:m-0"
            name={isExpanded ? icons.COLLAPSE : icons.EXPAND}
            title={
              isExpanded ? translate('HideEpisodes') : translate('ShowEpisodes')
            }
            size={24}
          />
          {isSmallScreen ? null : <span>&nbsp;</span>}
        </Link>

        {isSmallScreen ? (
          <Menu
            className={classNames(MENU_CLASS, 'flex-[0_0_45px]')}
            alignMenu={align.RIGHT}
            enforceMaxHeight={false}
          >
            <MenuButton>
              <Icon name={icons.ACTIONS} size={22} />
            </MenuButton>

            <MenuContent
              className={classNames(
                MENU_CONTENT_CLASS,
                'whitespace-nowrap text-[14px]'
              )}
            >
              <MenuItem
                isDisabled={
                  isSearching || !hasMonitoredEpisodes || !seriesMonitored
                }
                onPress={handleSearchPress}
              >
                <SpinnerIcon
                  className="mr-[8px]"
                  name={icons.SEARCH}
                  isSpinning={isSearching}
                />

                {translate('Search')}
              </MenuItem>

              <MenuItem
                isDisabled={!totalEpisodeCount}
                onPress={handleInteractiveSearchPress}
              >
                <Icon className="mr-[8px]" name={icons.INTERACTIVE} />

                {translate('InteractiveSearch')}
              </MenuItem>

              <MenuItem
                isDisabled={!episodeFileCount}
                onPress={handleOrganizePress}
              >
                <Icon className="mr-[8px]" name={icons.ORGANIZE} />

                {translate('PreviewRename')}
              </MenuItem>

              <MenuItem
                isDisabled={!episodeFileCount}
                onPress={handleManageEpisodesPress}
              >
                <Icon className="mr-[8px]" name={icons.EPISODE_FILE} />

                {translate('ManageEpisodes')}
              </MenuItem>

              <MenuItem
                isDisabled={!totalEpisodeCount}
                onPress={handleHistoryPress}
              >
                <Icon className="mr-[8px]" name={icons.HISTORY} />

                {translate('History')}
              </MenuItem>

              <MenuItem
                isDisabled={!episodeFileCount}
                onPress={handleCompressPress}
              >
                <Icon className="mr-[8px]" name={icons.GPU} />

                {translate('TranscodeSeason')}
              </MenuItem>
            </MenuContent>
          </Menu>
        ) : (
          <div className={ACTIONS_CLASS}>
            <SpinnerIconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.SEARCH}
              title={
                hasMonitoredEpisodes && seriesMonitored
                  ? translate('SearchForMonitoredEpisodesSeason')
                  : translate('NoMonitoredEpisodesSeason')
              }
              size={24}
              isSpinning={isSearching}
              isDisabled={
                isSearching || !hasMonitoredEpisodes || !seriesMonitored
              }
              onPress={handleSearchPress}
            />

            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.INTERACTIVE}
              title={translate('InteractiveSearchSeason')}
              aria-label={translate('InteractiveSearchSeason')}
              size={24}
              isDisabled={!totalEpisodeCount}
              onPress={handleInteractiveSearchPress}
            />

            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.ORGANIZE}
              title={translate('PreviewRenameSeason')}
              aria-label={translate('PreviewRenameSeason')}
              size={24}
              isDisabled={!episodeFileCount}
              onPress={handleOrganizePress}
            />

            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.EPISODE_FILE}
              title={translate('ManageEpisodesSeason')}
              aria-label={translate('ManageEpisodesSeason')}
              size={24}
              isDisabled={!episodeFileCount}
              onPress={handleManageEpisodesPress}
            />

            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.HISTORY}
              title={translate('HistorySeason')}
              aria-label={translate('HistorySeason')}
              size={24}
              isDisabled={!totalEpisodeCount}
              onPress={handleHistoryPress}
            />

            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[30px]"
              name={icons.GPU}
              title={translate('TranscodeSeason')}
              aria-label={translate('TranscodeSeason')}
              size={24}
              isDisabled={!episodeFileCount}
              onPress={handleCompressPress}
            />
          </div>
        )}
      </div>

      <div>
        {isExpanded ? (
          <div className="pt-[15px] border-t border-solid border-[var(--borderColor)]">
            {items.length ? (
              <Table
                columns={columns}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={handleSortPress}
                onTableOptionChange={handleTableOptionChange}
              >
                <TableBody>
                  {items.map((item) => {
                    return (
                      <EpisodeRow
                        key={item.id}
                        columns={columns}
                        {...item}
                        isSaving={
                          isToggling && togglingEpisodeIds.includes(item.id)
                        }
                        onMonitorEpisodePress={handleMonitorEpisodePress}
                      />
                    );
                  })}
                </TableBody>
              </Table>
            ) : (
              <div className="mb-[15px] text-center">
                {translate('NoEpisodesInThisSeason')}
              </div>
            )}

            <div className="flex items-center justify-center py-[10px] px-[15px] w-full border-t border-solid border-[var(--borderColor)] rounded-br-[4px] rounded-bl-[4px] bg-[var(--collapseButtonBackgroundColor)]">
              <IconButton
                iconClassName="mb-[-4px]"
                name={icons.COLLAPSE}
                size={20}
                title={translate('HideEpisodes')}
                aria-label={translate('HideEpisodes')}
                onPress={handleExpandPress}
              />
            </div>
          </div>
        ) : null}
      </div>

      <OrganizePreviewModal
        isOpen={isOrganizeModalOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleOrganizeModalClose}
      />

      <InteractiveImportModal
        isOpen={isManageEpisodesOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        title={seasonNumberTitle}
        folder={path}
        initialSortKey="relativePath"
        initialSortDirection={sortDirections.DESCENDING}
        showSeries={false}
        allowSeriesChange={false}
        showDelete={true}
        showImportMode={false}
        modalTitle={translate('ManageEpisodes')}
        onModalClose={handleManageEpisodesModalClose}
      />

      <SeriesHistoryModal
        isOpen={isHistoryModalOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleHistoryModalClose}
      />

      <SeasonInteractiveSearchModal
        isOpen={isInteractiveSearchModalOpen}
        episodeCount={totalEpisodeCount}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleInteractiveSearchModalClose}
      />

      <CompressModal
        isOpen={isCompressModalOpen}
        seriesIds={[seriesId]}
        seasonNumbers={[seasonNumber]}
        onModalClose={handleCompressModalClose}
      />
    </div>
  );
}

export default SeriesDetailsSeason;
