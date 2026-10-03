import React from 'react';
import Modal from 'Components/Modal/Modal';
import EditMovieModalContent, {
  EditMovieModalContentProps,
} from './EditMovieModalContent';

interface EditMovieModalProps extends EditMovieModalContentProps {
  isOpen: boolean;
}

function EditMovieModal({
  isOpen,
  onModalClose,
  ...otherProps
}: EditMovieModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <EditMovieModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default EditMovieModal;
