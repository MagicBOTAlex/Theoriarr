import React from 'react';
import Modal from 'Components/Modal/Modal';
import LibraryDeleteModalContent, {
  LibraryDeleteModalContentProps,
} from './LibraryDeleteModalContent';

interface LibraryDeleteModalProps extends LibraryDeleteModalContentProps {
  isOpen: boolean;
}

function LibraryDeleteModal(props: LibraryDeleteModalProps) {
  const { isOpen, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <LibraryDeleteModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default LibraryDeleteModal;
