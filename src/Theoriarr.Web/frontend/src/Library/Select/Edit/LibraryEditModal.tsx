import React from 'react';
import Modal from 'Components/Modal/Modal';
import LibraryEditModalContent, {
  LibraryEditModalContentProps,
} from './LibraryEditModalContent';

interface LibraryEditModalProps extends LibraryEditModalContentProps {
  isOpen: boolean;
}

function LibraryEditModal({
  isOpen,
  selectedCount,
  onSavePress,
  onModalClose,
}: LibraryEditModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <LibraryEditModalContent
        selectedCount={selectedCount}
        onSavePress={onSavePress}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default LibraryEditModal;
