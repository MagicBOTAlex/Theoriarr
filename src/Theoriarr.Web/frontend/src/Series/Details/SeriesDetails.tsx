import moment from 'moment-timezone';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useCommands, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MetadataAttribution from 'Components/MetadataAttribution';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import useEpisodes from 'Episode/useEpisodes';
import useEpisodeFiles from 'EpisodeFile/useEpisodeFiles';
import usePrevious from 'Helpers/Hooks/usePrevious';
import {
  align,
  icons,
  kinds,
  sizes,
  sortDirections,
  tooltipPositions,
} from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import useCountryName from 'Internationalization/useCountryName';
import CompressModal from 'Library/Compression/CompressModal';
import OrganizePreviewModal from 'Organize/OrganizePreviewModal';
import DeleteSeriesModal from 'Series/Delete/DeleteSeriesModal';
import EditSeriesModal from 'Series/Edit/EditSeriesModal';
import SeriesHistoryModal from 'Series/History/SeriesHistoryModal';
import MonitoringOptionsModal from 'Series/MonitoringOptions/MonitoringOptionsModal';
import { Image, SeriesStatus, Statistics } from 'Series/Series';
import SeriesGenres from 'Series/SeriesGenres';
import SeriesPoster from 'Series/SeriesPoster';
import { getSeriesStatusDetails } from 'Series/SeriesStatus';
import useSeries, {
  useSingleSeries,
  useToggleSeriesMonitored,
} from 'Series/useSeries';
import QualityProfileName from 'Settings/Profiles/Quality/QualityProfileName';
import sortByProp from 'Utilities/Array/sortByProp';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import formatBytes from 'Utilities/Number/formatBytes';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import filterAlternateTitles from 'Utilities/Series/filterAlternateTitles';
import translate from 'Utilities/String/translate';
import toggleSelected from 'Utilities/Table/toggleSelected';
import SeriesAlternateTitles from './SeriesAlternateTitles';
import SeriesDetailsLinks from './SeriesDetailsLinks';
import SeriesDetailsProvider from './SeriesDetailsProvider';
import SeriesDetailsSeason from './SeriesDetailsSeason';
import SeriesProgressLabel from './SeriesProgressLabel';
import SeriesTags from './SeriesTags';

const RUNTIME_GENRES_CLASS = 'mr-[15px]';
const DETAIL_LABEL_CLASS = 'ml-[8px] font-light text-[17px]';

function getFanartUrl(images: Image[]) {
  return images.find((image) => image.coverType === 'fanart')?.url;
}

function getDateYear(date: string) {
  return moment.utc(date).format('YYYY');
}

function getRunningYears(
  status: SeriesStatus,
  year: number,
  lastAired: string | undefined
) {
  if (year === 0) {
    return null;
  }

  return status === 'ended' && lastAired
    ? `${year}-${getDateYear(lastAired)}`
    : `${year}-`;
}

interface ExpandedState {
  allExpanded: boolean;
  allCollapsed: boolean;
  seasons: Record<number, boolean>;
}

interface SeriesDetailsProps {
  seriesId: number;
}

function SeriesDetails({ seriesId }: SeriesDetailsProps) {
  const executeCommand = useExecuteCommand();

  const series = useSingleSeries(seriesId);
  const { toggleSeriesMonitored, isTogglingSeriesMonitored } =
    useToggleSeriesMonitored(seriesId);
  const { data: allSeries } = useSeries();

  const {
    isFetching: isEpisodesFetching,
    isFetched: isEpisodesFetched,
    error: episodesError,
    data,
    refetch: refetchEpisodes,
  } = useEpisodes({ seriesId });

  const { hasEpisodes, hasMonitoredEpisodes } = useMemo(() => {
    return {
      hasEpisodes: data.length > 0,
      hasMonitoredEpisodes: data.some((e) => e.monitored),
    };
  }, [data]);

  const {
    isFetching: isEpisodeFilesFetching,
    isFetched: isEpisodeFilesFetched,
    error: episodeFilesError,
    hasEpisodeFiles,
    refetch: refetchEpisodeFiles,
  } = useEpisodeFiles({ seriesId });

  const { data: commands } = useCommands();

  const { isRefreshing, isRenaming, isSearching } = useMemo(() => {
    const seriesRefreshingCommand = findCommand(commands, {
      name: CommandNames.RefreshSeries,
    });

    const isSeriesRefreshingCommandExecuting = isCommandExecuting(
      seriesRefreshingCommand
    );

    const allSeriesRefreshing =
      isSeriesRefreshingCommandExecuting &&
      seriesRefreshingCommand &&
      (!('seriesIds' in seriesRefreshingCommand.body) ||
        seriesRefreshingCommand.body.seriesIds.length === 0);

    const isSeriesRefreshing =
      isSeriesRefreshingCommandExecuting &&
      seriesRefreshingCommand &&
      'seriesIds' in seriesRefreshingCommand.body &&
      seriesRefreshingCommand.body.seriesIds.includes(seriesId);

    const isSearchingExecuting = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.SeriesSearch,
        seriesId,
      })
    );

    const isRenamingFiles = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.RenameFiles,
        seriesId,
      })
    );

    const isRenamingSeriesCommand = findCommand(commands, {
      name: CommandNames.RenameSeries,
    });

    const isRenamingSeries =
      isCommandExecuting(isRenamingSeriesCommand) &&
      isRenamingSeriesCommand &&
      'seriesIds' in isRenamingSeriesCommand.body &&
      isRenamingSeriesCommand.body.seriesIds.includes(seriesId);

    return {
      isRefreshing: isSeriesRefreshing || allSeriesRefreshing,
      isRenaming: isRenamingFiles || isRenamingSeries,
      isSearching: isSearchingExecuting,
    };
  }, [seriesId, commands]);

  const { nextSeries, previousSeries } = useMemo(() => {
    const sortedSeries = [...allSeries].sort(sortByProp('sortTitle'));
    const seriesIndex = sortedSeries.findIndex(
      (series) => series.id === seriesId
    );

    if (seriesIndex === -1) {
      return {
        nextSeries: undefined,
        previousSeries: undefined,
      };
    }

    const nextSeries = sortedSeries[seriesIndex + 1] ?? sortedSeries[0];
    const previousSeries =
      sortedSeries[seriesIndex - 1] ?? sortedSeries[sortedSeries.length - 1];

    return {
      nextSeries: {
        title: nextSeries.title,
        titleSlug: nextSeries.titleSlug,
      },
      previousSeries: {
        title: previousSeries.title,
        titleSlug: previousSeries.titleSlug,
      },
    };
  }, [seriesId, allSeries]);

  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isManageEpisodesOpen, setIsManageEpisodesOpen] = useState(false);
  const [isEditSeriesModalOpen, setIsEditSeriesModalOpen] = useState(false);
  const [isDeleteSeriesModalOpen, setIsDeleteSeriesModalOpen] = useState(false);
  const [isSeriesHistoryModalOpen, setIsSeriesHistoryModalOpen] =
    useState(false);
  const [isMonitorOptionsModalOpen, setIsMonitorOptionsModalOpen] =
    useState(false);
  const [isCompressModalOpen, setIsCompressModalOpen] = useState(false);
  const [expandedState, setExpandedState] = useState<ExpandedState>({
    allExpanded: false,
    allCollapsed: false,
    seasons: {},
  });
  const wasRefreshing = usePrevious(isRefreshing);
  const wasRenaming = usePrevious(isRenaming);

  const alternateTitles = useMemo(() => {
    if (!series) {
      return [];
    }

    return filterAlternateTitles(
      series.alternateTitles,
      series.title,
      series.useSceneNumbering
    );
  }, [series]);

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

  const handleEditSeriesPress = useCallback(() => {
    setIsEditSeriesModalOpen(true);
  }, []);

  const handleEditSeriesModalClose = useCallback(() => {
    setIsEditSeriesModalOpen(false);
  }, []);

  const handleDeleteSeriesPress = useCallback(() => {
    setIsEditSeriesModalOpen(false);
    setIsDeleteSeriesModalOpen(true);
  }, []);

  const handleDeleteSeriesModalClose = useCallback(() => {
    setIsDeleteSeriesModalOpen(false);
  }, []);

  const handleSeriesHistoryPress = useCallback(() => {
    setIsSeriesHistoryModalOpen(true);
  }, []);

  const handleSeriesHistoryModalClose = useCallback(() => {
    setIsSeriesHistoryModalOpen(false);
  }, []);

  const handleMonitorOptionsPress = useCallback(() => {
    setIsMonitorOptionsModalOpen(true);
  }, []);

  const handleMonitorOptionsClose = useCallback(() => {
    setIsMonitorOptionsModalOpen(false);
  }, []);

  const handleCompressPress = useCallback(() => {
    setIsCompressModalOpen(true);
  }, []);

  const handleCompressModalClose = useCallback(() => {
    setIsCompressModalOpen(false);
  }, []);

  const handleExpandAllPress = useCallback(() => {
    const expandAll = !expandedState.allExpanded;

    const newSeasons = Object.keys(expandedState.seasons).reduce<
      Record<number | string, boolean>
    >((acc, item) => {
      acc[item] = expandAll;
      return acc;
    }, {});

    setExpandedState({
      allExpanded: expandAll,
      allCollapsed: !expandAll,
      seasons: newSeasons,
    });
  }, [expandedState]);

  const handleExpandPress = useCallback(
    (seasonNumber: number, isExpanded: boolean) => {
      setExpandedState((state) => {
        const { allExpanded, allCollapsed } = state;

        const convertedState = {
          allSelected: allExpanded,
          allUnselected: allCollapsed,
          selectedState: state.seasons,
          lastToggled: null,
        };

        const newState = toggleSelected(
          convertedState,
          [],
          seasonNumber,
          isExpanded,
          false
        );

        return {
          allExpanded: newState.allSelected,
          allCollapsed: newState.allUnselected,
          seasons: newState.selectedState,
        };
      });
    },
    []
  );

  const handleMonitorTogglePress = useCallback(
    (value: boolean) => {
      toggleSeriesMonitored({
        monitored: value,
      });
    },
    [toggleSeriesMonitored]
  );

  const handleRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshSeries,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeriesSearch,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const populate = useCallback(() => {
    refetchEpisodes();
    refetchEpisodeFiles();
  }, [refetchEpisodes, refetchEpisodeFiles]);

  const originalCountryName = useCountryName(series?.originalCountry);

  useEffect(() => {
    populate();
  }, [populate]);

  useEffect(() => {
    registerPagePopulator(populate, ['seriesUpdated']);

    return () => {
      unregisterPagePopulator(populate);
    };
  }, [populate]);

  useEffect(() => {
    if ((!isRefreshing && wasRefreshing) || (!isRenaming && wasRenaming)) {
      populate();
    }
  }, [isRefreshing, wasRefreshing, isRenaming, wasRenaming, populate]);

  if (!series) {
    return null;
  }

  const {
    tvdbId,
    tvMazeId,
    imdbId,
    tmdbId,
    title,
    runtime,
    ratings,
    path,
    statistics = {} as Statistics,
    qualityProfileId,
    monitored,
    status,
    network,
    originalLanguage,
    overview,
    images,
    seasons,
    genres,
    tags,
    year,
    lastAired,
  } = series;

  const { episodeCount = 0, episodeFileCount = 0, sizeOnDisk = 0 } = statistics;

  const statusDetails = getSeriesStatusDetails(status);
  const runningYears = getRunningYears(status, year, lastAired);

  let episodeFilesCountMessage = translate('SeriesDetailsNoEpisodeFiles');

  if (episodeFileCount === 1) {
    episodeFilesCountMessage = translate('SeriesDetailsOneEpisodeFile');
  } else if (episodeFileCount > 1) {
    episodeFilesCountMessage = translate('SeriesDetailsCountEpisodeFiles', {
      episodeFileCount,
    });
  }

  let expandIcon = icons.EXPAND_INDETERMINATE;

  if (expandedState.allExpanded) {
    expandIcon = icons.COLLAPSE;
  } else if (expandedState.allCollapsed) {
    expandIcon = icons.EXPAND;
  }

  const fanartUrl = getFanartUrl(images);
  const isFetching = isEpisodesFetching || isEpisodeFilesFetching;
  const isPopulated = isEpisodesFetched && isEpisodeFilesFetched;

  return (
    <SeriesDetailsProvider seriesId={seriesId}>
      <PageContent title={title}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('RefreshAndScan')}
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              title={translate('RefreshAndScanTooltip')}
              isSpinning={isRefreshing}
              onPress={handleRefreshPress}
            />

            <PageToolbarButton
              label={translate('SearchMonitored')}
              iconName={icons.SEARCH}
              isDisabled={!monitored || !hasMonitoredEpisodes || !hasEpisodes}
              isSpinning={isSearching}
              title={
                hasMonitoredEpisodes
                  ? undefined
                  : translate('NoMonitoredEpisodes')
              }
              onPress={handleSearchPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('PreviewRename')}
              iconName={icons.ORGANIZE}
              isDisabled={!hasEpisodeFiles}
              onPress={handleOrganizePress}
            />

            <PageToolbarButton
              label={translate('ManageEpisodes')}
              iconName={icons.EPISODE_FILE}
              onPress={handleManageEpisodesPress}
            />

            <PageToolbarButton
              label={translate('TranscodeSeries')}
              iconName={icons.GPU}
              isDisabled={!hasEpisodeFiles}
              onPress={handleCompressPress}
            />

            <PageToolbarButton
              label={translate('History')}
              iconName={icons.HISTORY}
              isDisabled={!hasEpisodes}
              onPress={handleSeriesHistoryPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('EpisodeMonitoring')}
              iconName={icons.MONITORED}
              onPress={handleMonitorOptionsPress}
            />

            <PageToolbarButton
              label={translate('Edit')}
              iconName={icons.EDIT}
              onPress={handleEditSeriesPress}
            />

            <PageToolbarButton
              label={translate('Delete')}
              iconName={icons.DELETE}
              onPress={handleDeleteSeriesPress}
            />
          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <PageToolbarButton
              label={
                expandedState.allExpanded
                  ? translate('CollapseAll')
                  : translate('ExpandAll')
              }
              iconName={expandIcon}
              onPress={handleExpandAllPress}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody innerClassName="p-0">
          <div className="relative z-0 w-full">
            <div
              className="absolute [z-index:-1] w-full h-full bg-cover"
              style={
                fanartUrl ? { backgroundImage: `url(${fanartUrl})` } : undefined
              }
            >
              <div className="absolute w-full h-full bg-[var(--black)] opacity-70" />
            </div>

            <div className="flex p-[30px] w-full h-full text-[var(--white)] gap-[35px]">
              <SeriesPoster
                className="shrink-0 w-[250px] h-[368px] max-[1200px]:hidden"
                images={images}
                size={500}
                lazy={false}
                title={title}
              />

              <div className="overflow-hidden w-full">
                <div className="relative flex justify-between flex-[0_0_auto]">
                  <div className="flex mb-[5px]">
                    <div className="self-center mr-[10px]">
                      <MonitorToggleButton
                        className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[40px] hover:text-[var(--iconButtonHoverLightColor)]!"
                        monitored={monitored}
                        isSaving={isTogglingSeriesMonitored}
                        size={40}
                        onPress={handleMonitorTogglePress}
                      />
                    </div>

                    <div className="overflow-auto max-h-[calc(3*60px)] [text-wrap:balance] font-light text-[50px] leading-[60px] [-webkit-line-clamp:3] [line-clamp:3] max-[768px]:overflow-hidden max-[768px]:max-h-[calc(3*30px)] max-[768px]:text-[30px] max-[768px]:leading-[30px]">
                      {title}
                    </div>

                    {alternateTitles.length ? (
                      <div className="self-end mb-[10px] ml-[20px]">
                        <Popover
                          anchor={
                            <Icon name={icons.ALTERNATE_TITLES} size={20} />
                          }
                          title={translate('AlternateTitles')}
                          body={
                            <SeriesAlternateTitles
                              alternateTitles={alternateTitles}
                            />
                          }
                          position={tooltipPositions.BOTTOM}
                        />
                      </div>
                    ) : null}
                  </div>

                  <div className="absolute right-0 whitespace-nowrap">
                    {previousSeries ? (
                      <IconButton
                        className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] ml-[5px] w-[30px] text-[#e1e2e3]! whitespace-nowrap hover:text-[var(--iconButtonHoverLightColor)]!"
                        name={icons.ARROW_LEFT}
                        size={30}
                        title={translate('SeriesDetailsGoTo', {
                          title: previousSeries.title,
                        })}
                        aria-label={translate('SeriesDetailsGoTo', {
                          title: previousSeries.title,
                        })}
                        to={`/series/${previousSeries.titleSlug}`}
                      />
                    ) : null}

                    {nextSeries ? (
                      <IconButton
                        className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] ml-[5px] w-[30px] text-[#e1e2e3]! whitespace-nowrap hover:text-[var(--iconButtonHoverLightColor)]!"
                        name={icons.ARROW_RIGHT}
                        size={30}
                        title={translate('SeriesDetailsGoTo', {
                          title: nextSeries.title,
                        })}
                        aria-label={translate('SeriesDetailsGoTo', {
                          title: nextSeries.title,
                        })}
                        to={`/series/${nextSeries.titleSlug}`}
                      />
                    ) : null}
                  </div>
                </div>

                <div className="mb-[8px] font-light text-[20px]">
                  <div>
                    {runtime ? (
                      <span className={RUNTIME_GENRES_CLASS}>
                        {translate('SeriesDetailsRuntime', { runtime })}
                      </span>
                    ) : null}

                    {ratings.value ? (
                      <HeartRating
                        rating={ratings.value}
                        votes={ratings.votes}
                        iconSize={20}
                      />
                    ) : null}

                    <SeriesGenres
                      className={RUNTIME_GENRES_CLASS}
                      genres={genres}
                    />

                    <span>{runningYears}</span>
                  </div>
                </div>

                <div>
                  <Label
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon name={icons.FOLDER} size={17} />
                      <span className={DETAIL_LABEL_CLASS}>{path}</span>
                    </div>
                  </Label>

                  <Tooltip
                    accessibleLabel={translate('Links')}
                    anchor={
                      <Label
                        className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon name={icons.DRIVE} size={17} />

                          <span className={DETAIL_LABEL_CLASS}>
                            {formatBytes(sizeOnDisk)}
                          </span>
                        </div>
                      </Label>
                    }
                    tooltip={<span>{episodeFilesCountMessage}</span>}
                    kind={kinds.INVERSE}
                    position={tooltipPositions.BOTTOM}
                  />

                  <Label
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                    title={translate('QualityProfile')}
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon name={icons.PROFILE} size={17} />
                      <span className={DETAIL_LABEL_CLASS}>
                        <QualityProfileName
                          qualityProfileId={qualityProfileId}
                        />
                      </span>
                    </div>
                  </Label>

                  <Label
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon
                        name={monitored ? icons.MONITORED : icons.UNMONITORED}
                        size={17}
                      />
                      <span className={DETAIL_LABEL_CLASS}>
                        {monitored
                          ? translate('Monitored')
                          : translate('Unmonitored')}
                      </span>
                    </div>
                  </Label>

                  <Label
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                    title={statusDetails.message}
                    size={sizes.LARGE}
                    kind={status === 'deleted' ? kinds.INVERSE : undefined}
                  >
                    <div>
                      <Icon name={statusDetails.icon} size={17} />
                      <span className={DETAIL_LABEL_CLASS}>
                        {statusDetails.title}
                      </span>
                    </div>
                  </Label>

                  {originalLanguage?.name ? (
                    <Label
                      className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                      title={translate('OriginalLanguage')}
                      size={sizes.LARGE}
                    >
                      <div>
                        <Icon name={icons.LANGUAGE} size={17} />
                        <span className={DETAIL_LABEL_CLASS}>
                          {originalLanguage.name}
                        </span>
                      </div>
                    </Label>
                  ) : null}

                  {originalCountryName ? (
                    <Label
                      className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                      title={translate('OriginalCountry')}
                      size={sizes.LARGE}
                    >
                      <div>
                        <Icon name={icons.GLOBE} size={17} />
                        <span className={DETAIL_LABEL_CLASS}>
                          {originalCountryName}
                        </span>
                      </div>
                    </Label>
                  ) : null}

                  {network ? (
                    <Label
                      className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                      title={translate('Network')}
                      size={sizes.LARGE}
                    >
                      <div>
                        <Icon name={icons.NETWORK} size={17} />
                        <span className={DETAIL_LABEL_CLASS}>{network}</span>
                      </div>
                    </Label>
                  ) : null}

                  <Tooltip
                    anchor={
                      <Label
                        className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon name={icons.EXTERNAL_LINK} size={17} />
                          <span className={DETAIL_LABEL_CLASS}>
                            {translate('Links')}
                          </span>
                        </div>
                      </Label>
                    }
                    contentRole="dialog"
                    tooltip={
                      <SeriesDetailsLinks
                        tvdbId={tvdbId}
                        tvMazeId={tvMazeId}
                        imdbId={imdbId}
                        tmdbId={tmdbId}
                      />
                    }
                    kind={kinds.INVERSE}
                    position={tooltipPositions.BOTTOM}
                  />

                  {tags.length ? (
                    <Tooltip
                      anchor={
                        <Label
                          className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default my-[5px] mr-[10px] ml-0"
                          size={sizes.LARGE}
                        >
                          <Icon name={icons.TAGS} size={17} />

                          <span className={DETAIL_LABEL_CLASS}>
                            {translate('Tags')}
                          </span>
                        </Label>
                      }
                      tooltip={<SeriesTags seriesId={seriesId} />}
                      kind={kinds.INVERSE}
                      position={tooltipPositions.BOTTOM}
                    />
                  ) : null}

                  <SeriesProgressLabel
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! text-[17px]"
                    seriesId={seriesId}
                    monitored={monitored}
                    episodeCount={episodeCount}
                    episodeFileCount={episodeFileCount}
                  />
                </div>

                <div className="flex-[1_0_0] mt-[8px] min-h-0 [text-wrap:balance] text-[15px]">
                  {overview}
                </div>

                <MetadataAttribution />
              </div>
            </div>
          </div>

          <div className="p-[20px] max-[768px]:p-[20px_0]">
            {!isPopulated && !episodesError && !episodeFilesError ? (
              <LoadingIndicator />
            ) : null}

            {!isFetching && episodesError ? (
              <Alert kind={kinds.DANGER}>
                {translate('EpisodesLoadError')}
              </Alert>
            ) : null}

            {!isFetching && episodeFilesError ? (
              <Alert kind={kinds.DANGER}>
                {translate('EpisodeFilesLoadError')}
              </Alert>
            ) : null}

            {isPopulated && !!seasons.length ? (
              <div>
                {seasons
                  .slice(0)
                  .reverse()
                  .map((season) => {
                    return (
                      <SeriesDetailsSeason
                        key={season.seasonNumber}
                        seriesId={seriesId}
                        {...season}
                        isExpanded={expandedState.seasons[season.seasonNumber]}
                        onExpandPress={handleExpandPress}
                      />
                    );
                  })}
              </div>
            ) : null}

            {isPopulated && !seasons.length ? (
              <Alert kind={kinds.WARNING}>
                {translate('NoEpisodeInformation')}
              </Alert>
            ) : null}
          </div>

          <OrganizePreviewModal
            isOpen={isOrganizeModalOpen}
            seriesId={seriesId}
            onModalClose={handleOrganizeModalClose}
          />

          <InteractiveImportModal
            isOpen={isManageEpisodesOpen}
            seriesId={seriesId}
            title={title}
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
            isOpen={isSeriesHistoryModalOpen}
            seriesId={seriesId}
            onModalClose={handleSeriesHistoryModalClose}
          />

          <EditSeriesModal
            isOpen={isEditSeriesModalOpen}
            seriesId={seriesId}
            onModalClose={handleEditSeriesModalClose}
            onDeleteSeriesPress={handleDeleteSeriesPress}
          />

          <DeleteSeriesModal
            isOpen={isDeleteSeriesModalOpen}
            seriesId={seriesId}
            onModalClose={handleDeleteSeriesModalClose}
          />

          <MonitoringOptionsModal
            isOpen={isMonitorOptionsModalOpen}
            seriesId={seriesId}
            onModalClose={handleMonitorOptionsClose}
          />

          <CompressModal
            isOpen={isCompressModalOpen}
            seriesIds={[seriesId]}
            onModalClose={handleCompressModalClose}
          />
        </PageContentBody>
      </PageContent>
    </SeriesDetailsProvider>
  );
}

export default SeriesDetails;
