import React from 'react';
import Modal from 'Components/Modal/Modal';
import SeasonInteractiveSearchModalContent, {
  SeasonInteractiveSearchModalContentProps,
} from './SeasonInteractiveSearchModalContent';

interface SeasonInteractiveSearchModalProps
  extends SeasonInteractiveSearchModalContentProps {
  isOpen: boolean;
}

const MODAL_STYLE = {
  width: 'min(1600px, 94vw)',
  height: '86vh',
};

function SeasonInteractiveSearchModal(
  props: SeasonInteractiveSearchModalProps
) {
  const { isOpen, episodeCount, seriesId, seasonNumber, onModalClose } = props;

  return (
    <Modal
      isOpen={isOpen}
      style={MODAL_STYLE}
      closeOnBackgroundClick={false}
      onModalClose={onModalClose}
    >
      <SeasonInteractiveSearchModalContent
        episodeCount={episodeCount}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SeasonInteractiveSearchModal;
