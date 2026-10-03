import React from 'react';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Scroller from 'Components/Scroller/Scroller';
import { scrollDirections } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const DETAILS_TEXT_CLASS = `block mb-[10.5px] p-[10px] border border-solid border-[#ccc] rounded-[4px] bg-[var(--logEventsBackgroundColor)] text-[var(--textColor)] whitespace-pre break-words break-all text-[13px] [font-family:'Ubuntu_Mono','Menlo','Monaco','Consolas','Courier_New',monospace] leading-[1.52857143]`;

interface LogsTableDetailsModalProps {
  isOpen: boolean;
  message: string;
  exception?: string;
  onModalClose: () => void;
}

function LogsTableDetailsModal({
  isOpen,
  message,
  exception,
  onModalClose,
}: LogsTableDetailsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{translate('Details')}</ModalHeader>

        <ModalBody>
          <div>{translate('Message')}</div>

          <Scroller
            className={DETAILS_TEXT_CLASS}
            scrollDirection={scrollDirections.HORIZONTAL}
          >
            {message}
          </Scroller>

          {exception ? (
            <div>
              <div>{translate('Exception')}</div>
              <Scroller
                className={DETAILS_TEXT_CLASS}
                scrollDirection={scrollDirections.HORIZONTAL}
              >
                {exception}
              </Scroller>
            </div>
          ) : null}
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default LogsTableDetailsModal;
