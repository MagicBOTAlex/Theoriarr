import React from 'react';
import Modal from 'Components/Modal/Modal';
import CompressModalContent, {
  CompressModalContentProps,
} from './CompressModalContent';

interface CompressModalProps extends CompressModalContentProps {
  isOpen: boolean;
}

function CompressModal(props: CompressModalProps) {
  const { isOpen, ...rest } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={rest.onModalClose}>
      <CompressModalContent {...rest} />
    </Modal>
  );
}

export default CompressModal;
