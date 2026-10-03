import React from 'react';
import Modal from 'Components/Modal/Modal';
import TranscodeProfileModalContent, {
  TranscodeProfileModalContentProps,
} from './TranscodeProfileModalContent';

interface TranscodeProfileModalProps extends TranscodeProfileModalContentProps {
  isOpen: boolean;
}

function TranscodeProfileModal(props: TranscodeProfileModalProps) {
  const { isOpen, ...rest } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={rest.onModalClose}>
      <TranscodeProfileModalContent {...rest} />
    </Modal>
  );
}

export default TranscodeProfileModal;
