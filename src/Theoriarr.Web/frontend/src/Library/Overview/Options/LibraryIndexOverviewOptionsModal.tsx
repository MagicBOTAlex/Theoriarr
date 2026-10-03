import React from 'react';
import Modal from 'Components/Modal/Modal';
import LibraryIndexOverviewOptionsModalContent from './LibraryIndexOverviewOptionsModalContent';

interface LibraryIndexOverviewOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): void;
}

function LibraryIndexOverviewOptionsModal({
  isOpen,
  onModalClose,
  ...otherProps
}: LibraryIndexOverviewOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <LibraryIndexOverviewOptionsModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default LibraryIndexOverviewOptionsModal;
