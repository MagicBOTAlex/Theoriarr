import React from 'react';
import Modal from 'Components/Modal/Modal';
import DeleteSeriesFilesModalContent, {
  DeleteSeriesFilesModalContentProps,
} from './DeleteSeriesFilesModalContent';

interface DeleteSeriesFilesModalProps
  extends DeleteSeriesFilesModalContentProps {
  isOpen: boolean;
}

function DeleteSeriesFilesModal(props: DeleteSeriesFilesModalProps) {
  const { isOpen, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <DeleteSeriesFilesModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default DeleteSeriesFilesModal;
