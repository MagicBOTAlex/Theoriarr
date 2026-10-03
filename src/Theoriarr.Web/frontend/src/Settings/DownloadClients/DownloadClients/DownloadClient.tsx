import React, { useCallback, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import { icons, kinds } from 'Helpers/Props';
import { useTagList } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import EditDownloadClientModal from './EditDownloadClientModal';
import { useDeleteDownloadClient } from './useDownloadClients';

const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';

interface DownloadClientProps {
  id: number;
  name: string;
  protocol: DownloadProtocol;
  enable: boolean;
  priority: number;
  tags: number[];
  onCloneDownloadClientPress: (id: number) => void;
}

function DownloadClient({
  id,
  name,
  protocol,
  enable,
  priority,
  tags,
  onCloneDownloadClientPress,
}: DownloadClientProps) {
  const tagList = useTagList();
  const { deleteDownloadClient } = useDeleteDownloadClient(id);

  const [isEditDownloadClientModalOpen, setIsEditDownloadClientModalOpen] =
    useState(false);
  const [isDeleteDownloadClientModalOpen, setIsDeleteDownloadClientModalOpen] =
    useState(false);

  const handleEditDownloadClientPress = useCallback(() => {
    setIsEditDownloadClientModalOpen(true);
  }, []);

  const handleEditDownloadClientModalClose = useCallback(() => {
    setIsEditDownloadClientModalOpen(false);
  }, []);

  const handleDeleteDownloadClientPress = useCallback(() => {
    setIsEditDownloadClientModalOpen(false);
    setIsDeleteDownloadClientModalOpen(true);
  }, []);

  const handleDeleteDownloadClientModalClose = useCallback(() => {
    setIsDeleteDownloadClientModalOpen(false);
  }, []);

  const handleConfirmDeleteDownloadClient = useCallback(() => {
    deleteDownloadClient();
  }, [deleteDownloadClient]);

  const handleCloneDownloadClientPress = useCallback(() => {
    onCloneDownloadClientPress(id);
  }, [id, onCloneDownloadClientPress]);

  return (
    <Card
      className="w-[290px]"
      overlayContent={true}
      aria-label={translate('EditDownloadClientName', { name })}
      onPress={handleEditDownloadClientPress}
    >
      <div className="flex justify-between">
        <div className={NAME_CLASS}>{name}</div>

        <IconButton
          className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] h-[36px]"
          title={translate('CloneDownloadClient')}
          aria-label={translate('CloneDownloadClient')}
          name={icons.CLONE}
          onPress={handleCloneDownloadClientPress}
        />
      </div>

      <div className="flex flex-wrap mt-[5px]">
        <ProtocolLabel protocol={protocol} />

        {enable ? (
          <Label kind={kinds.SUCCESS}>{translate('Enabled')}</Label>
        ) : (
          <Label kind={kinds.DISABLED} outline={true}>
            {translate('Disabled')}
          </Label>
        )}

        {priority > 1 ? (
          <Label kind={kinds.DISABLED} outline={true}>
            {translate('PrioritySettings', { priority })}
          </Label>
        ) : null}
      </div>

      <TagList tags={tags} tagList={tagList} />

      <EditDownloadClientModal
        id={id}
        isOpen={isEditDownloadClientModalOpen}
        onModalClose={handleEditDownloadClientModalClose}
        onDeleteDownloadClientPress={handleDeleteDownloadClientPress}
      />

      <ConfirmModal
        isOpen={isDeleteDownloadClientModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteDownloadClient')}
        message={translate('DeleteDownloadClientMessageText', { name })}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDeleteDownloadClient}
        onCancel={handleDeleteDownloadClientModalClose}
      />
    </Card>
  );
}

export default DownloadClient;
