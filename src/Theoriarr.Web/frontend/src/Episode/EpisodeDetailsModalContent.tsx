import React, { useCallback, useState } from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import Episode from 'Episode/Episode';
import EpisodeDetailsTab from 'Episode/EpisodeDetailsTab';
import episodeEntities from 'Episode/episodeEntities';
import useEpisode, {
  EpisodeEntity,
  getQueryKey,
  useToggleEpisodesMonitored,
} from 'Episode/useEpisode';
import { useClearReleasesOnUnmount } from 'InteractiveSearch/useReleases';
import Series from 'Series/Series';
import { useSingleSeries } from 'Series/useSeries';
import translate from 'Utilities/String/translate';
import EpisodeHistory from './History/EpisodeHistory';
import EpisodeSearch from './Search/EpisodeSearch';
import SeasonEpisodeNumber from './SeasonEpisodeNumber';
import EpisodeSummary from './Summary/EpisodeSummary';

const TAB_CLASS =
  'relative bottom-[-1px] inline-block px-[12px] py-[6px] border border-solid border-transparent border-t-0 list-none cursor-pointer';
const SELECTED_TAB_CLASS =
  'border-[var(--borderColor)]! rounded-[0_0_5px_5px] bg-[rgba(239,239,239,0.4)] text-[var(--black)]';

const TABS: EpisodeDetailsTab[] = ['details', 'history', 'search'];

export interface EpisodeDetailsModalContentProps {
  episodeId: number;
  episodeEntity: EpisodeEntity;
  seriesId: number;
  episodeTitle: string;
  showOpenSeriesButton?: boolean;
  selectedTab?: EpisodeDetailsTab;
  startInteractiveSearch?: boolean;
  onTabChange(isSearch: boolean): void;
  onModalClose(): void;
}

function EpisodeDetailsModalContent({
  episodeId,
  episodeEntity = episodeEntities.EPISODES,
  seriesId,
  episodeTitle,
  showOpenSeriesButton = false,
  startInteractiveSearch = false,
  selectedTab = 'details',
  onTabChange,
  onModalClose,
}: EpisodeDetailsModalContentProps) {
  const [currentlySelectedTab, setCurrentlySelectedTab] = useState(selectedTab);

  const {
    title: seriesTitle,
    titleSlug,
    monitored: seriesMonitored,
    seriesType,
  } = useSingleSeries(seriesId) as Series;

  const {
    episodeFileId,
    seasonNumber,
    episodeNumber,
    absoluteEpisodeNumber,
    airDate,
    monitored,
  } = useEpisode(episodeId, episodeEntity) as Episode;

  const { toggleEpisodesMonitored, isToggling } = useToggleEpisodesMonitored(
    getQueryKey(episodeEntity)!
  );

  useClearReleasesOnUnmount({ episodeId });

  const handleTabSelect = useCallback(
    (selectedIndex: number) => {
      const tab = TABS[selectedIndex];
      onTabChange(tab === 'search');
      setCurrentlySelectedTab(tab);
    },
    [onTabChange]
  );

  const handleMonitorEpisodePress = useCallback(
    (monitored: boolean) => {
      toggleEpisodesMonitored({
        episodeIds: [episodeId],
        monitored,
      });
    },
    [episodeId, toggleEpisodesMonitored]
  );

  const seriesLink = `/series/${titleSlug}`;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        <MonitorToggleButton
          monitored={monitored}
          size={18}
          isDisabled={!seriesMonitored}
          isSaving={isToggling}
          onPress={handleMonitorEpisodePress}
        />

        <span className="ml-[5px]">{seriesTitle}</span>

        <span className="mx-[5px]">-</span>

        <SeasonEpisodeNumber
          seasonNumber={seasonNumber}
          episodeNumber={episodeNumber}
          absoluteEpisodeNumber={absoluteEpisodeNumber}
          airDate={airDate}
          seriesType={seriesType}
        />

        <span className="mx-[5px]">-</span>

        {episodeTitle}
      </ModalHeader>

      <ModalBody>
        <Tabs
          className="mt-[-32px]"
          selectedIndex={TABS.indexOf(currentlySelectedTab)}
          onSelect={handleTabSelect}
        >
          <TabList className="m-0 p-0">
            <Tab className={TAB_CLASS} selectedClassName={SELECTED_TAB_CLASS}>
              {translate('Details')}
            </Tab>

            <Tab className={TAB_CLASS} selectedClassName={SELECTED_TAB_CLASS}>
              {translate('History')}
            </Tab>

            <Tab className={TAB_CLASS} selectedClassName={SELECTED_TAB_CLASS}>
              {translate('Search')}
            </Tab>
          </TabList>

          <TabPanel>
            <div className="mt-[20px]">
              <EpisodeSummary
                episodeId={episodeId}
                episodeEntity={episodeEntity}
                episodeFileId={episodeFileId}
                seriesId={seriesId}
              />
            </div>
          </TabPanel>

          <TabPanel>
            <div className="mt-[20px]">
              <EpisodeHistory episodeId={episodeId} />
            </div>
          </TabPanel>

          <TabPanel>
            {/* Don't wrap in tabContent so we not have a top margin */}
            <EpisodeSearch
              episodeId={episodeId}
              startInteractiveSearch={startInteractiveSearch}
              onModalClose={onModalClose}
            />
          </TabPanel>
        </Tabs>
      </ModalBody>

      <ModalFooter>
        {showOpenSeriesButton && (
          <Button className="mr-auto" to={seriesLink} onPress={onModalClose}>
            {translate('OpenSeries')}
          </Button>
        )}

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default EpisodeDetailsModalContent;
