import React, { useCallback, useMemo } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import { splitSelectKeys } from '../../../MediaItem';
import useLibraryItems from '../../../useLibraryItems';

export interface DeleteSeriesFilesModalContentProps {
  onModalClose(): void;
}

function DeleteSeriesFilesModalContent({
  onModalClose,
}: DeleteSeriesFilesModalContentProps) {
  const { useSelectedIds } = useSelect();
  const selectedIds = useSelectedIds();
  const { items } = useLibraryItems();
  const executeCommand = useExecuteCommand();

  const { seriesIds } = useMemo(
    () => splitSelectKeys(selectedIds),
    [selectedIds]
  );

  const selectedSeries = useMemo(
    () =>
      items.filter(
        (item) => item.type === 'series' && seriesIds.includes(item.id)
      ),
    [items, seriesIds]
  );

  const episodeFileCount = selectedSeries.reduce(
    (acc, item) => acc + item.episodeFileCount,
    0
  );
  const sizeOnDisk = selectedSeries.reduce(
    (acc, item) => acc + item.sizeOnDisk,
    0
  );

  const onDeleteSeriesFilesConfirmed = useCallback(() => {
    if (seriesIds.length) {
      executeCommand({
        name: CommandNames.DeleteSeriesFiles,
        seriesIds,
      });
    }

    onModalClose();
  }, [seriesIds, executeCommand, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteSelectedSeriesFiles')}</ModalHeader>

      <ModalBody>
        <div className="mb-[10px]">
          {translate('DeleteSeriesFilesConfirmation', {
            count: seriesIds.length,
          })}
        </div>

        <div>
          {translate('Files')}: {episodeFileCount}
        </div>

        <div>
          {translate('TotalSize')}: {formatBytes(sizeOnDisk)}
        </div>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.DANGER} onPress={onDeleteSeriesFilesConfirmed}>
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default DeleteSeriesFilesModalContent;
