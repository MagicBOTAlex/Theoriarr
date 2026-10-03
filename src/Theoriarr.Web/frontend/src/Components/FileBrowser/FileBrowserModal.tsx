import classNames from 'classnames';
import React from 'react';
import Modal, { MODAL_CLASS } from 'Components/Modal/Modal';
import FileBrowserModalContent, {
  FileBrowserModalContentProps,
} from './FileBrowserModalContent';

interface FileBrowserModalProps extends FileBrowserModalContentProps {
  isOpen: boolean;
  onModalClose: () => void;
}

function FileBrowserModal(props: FileBrowserModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal
      className={classNames(MODAL_CLASS, 'h-[600px]')}
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <FileBrowserModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default FileBrowserModal;
