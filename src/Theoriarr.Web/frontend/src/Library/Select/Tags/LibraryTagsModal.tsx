import React from 'react';
import Modal from 'Components/Modal/Modal';
import LibraryTagsModalContent, {
  LibraryTagsModalContentProps,
} from './LibraryTagsModalContent';

interface LibraryTagsModalProps extends LibraryTagsModalContentProps {
  isOpen: boolean;
}

function LibraryTagsModal(props: LibraryTagsModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <LibraryTagsModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default LibraryTagsModal;
