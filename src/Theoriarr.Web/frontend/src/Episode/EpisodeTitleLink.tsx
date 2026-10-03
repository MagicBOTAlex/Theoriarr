import React, { useCallback, useState } from 'react';
import Link from 'Components/Link/Link';
import EpisodeDetailsModal from 'Episode/EpisodeDetailsModal';
import { EpisodeEntity } from 'Episode/useEpisode';
import FinaleType from './FinaleType';

const CONTAINER_CLASS = 'flex items-center';

interface EpisodeTitleLinkProps {
  episodeId: number;
  seriesId: number;
  episodeEntity: EpisodeEntity;
  episodeTitle: string;
  finaleType?: string;
  showOpenSeriesButton: boolean;
}

function EpisodeTitleLink(props: EpisodeTitleLinkProps) {
  const { episodeTitle, finaleType, ...otherProps } = props;
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);
  const handleLinkPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, [setIsDetailsModalOpen]);
  const handleModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, [setIsDetailsModalOpen]);

  return (
    <div className={CONTAINER_CLASS}>
      <Link
        className="hover:text-[var(--linkHoverColor)] hover:underline"
        onPress={handleLinkPress}
      >
        {episodeTitle}
      </Link>

      {finaleType ? <FinaleType finaleType={finaleType} /> : null}

      <EpisodeDetailsModal
        isOpen={isDetailsModalOpen}
        episodeTitle={episodeTitle}
        {...otherProps}
        onModalClose={handleModalClose}
      />
    </div>
  );
}

export default EpisodeTitleLink;
