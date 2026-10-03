import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditRemotePathMappingModal from './EditRemotePathMappingModal';
import { useDeleteRemotePathMapping } from './useRemotePathMappings';

interface RemotePathMappingProps {
  id: number;
  host: string;
  remotePath: string;
  localPath: string;
}

function RemotePathMapping({
  id,
  host,
  remotePath,
  localPath,
}: RemotePathMappingProps) {
  const { deleteRemotePathMapping } = useDeleteRemotePathMapping(id);

  const [
    isEditRemotePathMappingModalOpen,
    setIsEditRemotePathMappingModalOpen,
  ] = useState(false);

  const [
    isDeleteRemotePathMappingModalOpen,
    setIsDeleteRemotePathMappingModalOpen,
  ] = useState(false);

  const handleEditRemotePathMappingPress = useCallback(() => {
    setIsEditRemotePathMappingModalOpen(true);
  }, []);

  const handleEditRemotePathMappingModalClose = useCallback(() => {
    setIsEditRemotePathMappingModalOpen(false);
  }, []);

  const handleDeleteRemotePathMappingPress = useCallback(() => {
    setIsEditRemotePathMappingModalOpen(false);
    setIsDeleteRemotePathMappingModalOpen(true);
  }, []);

  const handleDeleteRemotePathMappingModalClose = useCallback(() => {
    setIsDeleteRemotePathMappingModalOpen(false);
  }, []);

  const handleConfirmDeleteRemotePathMapping = useCallback(() => {
    deleteRemotePathMapping();
  }, [deleteRemotePathMapping]);

  return (
    <div
      className={classNames(
        'mb-[10px] flex h-[30px] items-stretch border-b border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] leading-[30px]'
      )}
    >
      <div className="shrink basis-[300px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
        {host}
      </div>
      <div className="shrink basis-[400px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
        {remotePath}
      </div>
      <div className="shrink basis-[400px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
        {localPath}
      </div>

      <div className="flex shrink-0 grow basis-auto justify-end pr-[10px]">
        <IconButton
          name={icons.EDIT}
          aria-label={translate('EditRemotePathMapping')}
          title={translate('EditRemotePathMapping')}
          onPress={handleEditRemotePathMappingPress}
        />
      </div>

      <EditRemotePathMappingModal
        id={id}
        isOpen={isEditRemotePathMappingModalOpen}
        onModalClose={handleEditRemotePathMappingModalClose}
        onDeleteRemotePathMappingPress={handleDeleteRemotePathMappingPress}
      />

      <ConfirmModal
        isOpen={isDeleteRemotePathMappingModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteRemotePathMapping')}
        message={translate('DeleteRemotePathMappingMessageText')}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDeleteRemotePathMapping}
        onCancel={handleDeleteRemotePathMappingModalClose}
      />
    </div>
  );
}

export default RemotePathMapping;
