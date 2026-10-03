import React from 'react';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { scrollDirections } from 'Helpers/Props';
import InteractiveSearch from 'InteractiveSearch/InteractiveSearch';
import { useClearReleasesOnUnmount } from 'InteractiveSearch/useReleases';
import translate from 'Utilities/String/translate';

interface InteractiveSearchModalProps {
  movieId: number;
  movieTitle: string;
  isOpen: boolean;
  onModalClose: () => void;
}

const MODAL_STYLE = {
  width: 'min(1600px, 94vw)',
  height: '86vh',
};

function InteractiveSearchModal({
  movieId,
  movieTitle,
  isOpen,
  onModalClose,
}: InteractiveSearchModalProps) {
  useClearReleasesOnUnmount({ movieId });

  return (
    <Modal isOpen={isOpen} style={MODAL_STYLE} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {translate('InteractiveSearchModalHeaderTitle', {
            title: movieTitle,
          })}
        </ModalHeader>

        <ModalBody innerClassName="p-4" scrollDirection={scrollDirections.BOTH}>
          <InteractiveSearch type="movie" searchPayload={{ movieId }} />
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default InteractiveSearchModal;
