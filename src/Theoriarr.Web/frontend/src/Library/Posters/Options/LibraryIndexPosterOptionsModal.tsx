import React from 'react';
import Modal from 'Components/Modal/Modal';
import LibraryIndexPosterOptionsModalContent from './LibraryIndexPosterOptionsModalContent';

interface LibraryIndexPosterOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): unknown;
}

function LibraryIndexPosterOptionsModal({
  isOpen,
  onModalClose,
}: LibraryIndexPosterOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <LibraryIndexPosterOptionsModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default LibraryIndexPosterOptionsModal;
