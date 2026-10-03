import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import { icons, kinds } from 'Helpers/Props';
import { useTagList } from 'Tags/useTags';
import formatShortTimeSpan from 'Utilities/Date/formatShortTimeSpan';
import translate from 'Utilities/String/translate';
import EditImportListModal from './EditImportListModal';
import { useDeleteImportList } from './useImportLists';

const NAME_CONTAINER_CLASS = 'flex justify-between';
const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';
const ENABLED_CLASS = 'flex flex-wrap mt-[5px]';

interface ImportListProps {
  id: number;
  name: string;
  enableAutomaticAdd: boolean;
  tags: number[];
  tagExisting: boolean;
  minRefreshInterval: string;
  onCloneImportListPress: (id: number) => void;
}

function ImportList({
  id,
  name,
  enableAutomaticAdd,
  tags,
  minRefreshInterval,
  onCloneImportListPress,
}: ImportListProps) {
  const tagList = useTagList();
  const { deleteImportList } = useDeleteImportList(id);

  const [isEditImportListModalOpen, setIsEditImportListModalOpen] =
    useState(false);

  const [isDeleteImportListModalOpen, setIsDeleteImportListModalOpen] =
    useState(false);

  const handleEditImportListPress = useCallback(() => {
    setIsEditImportListModalOpen(true);
  }, []);

  const handleEditImportListModalClose = useCallback(() => {
    setIsEditImportListModalOpen(false);
  }, []);

  const handleDeleteImportListPress = useCallback(() => {
    setIsEditImportListModalOpen(false);
    setIsDeleteImportListModalOpen(true);
  }, []);

  const handleDeleteImportListModalClose = useCallback(() => {
    setIsDeleteImportListModalOpen(false);
  }, []);

  const handleConfirmDeleteImportList = useCallback(() => {
    deleteImportList();
  }, [deleteImportList]);

  const handleCloneImportListPress = useCallback(() => {
    onCloneImportListPress(id);
  }, [id, onCloneImportListPress]);

  return (
    <Card
      className="w-[290px]"
      overlayContent={true}
      aria-label={translate('EditImportListName', { name })}
      onPress={handleEditImportListPress}
    >
      <div className={NAME_CONTAINER_CLASS}>
        <div className={NAME_CLASS}>{name}</div>

        <IconButton
          className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] h-[36px]"
          title={translate('CloneImportList')}
          aria-label={translate('CloneImportList')}
          name={icons.CLONE}
          onPress={handleCloneImportListPress}
        />
      </div>

      <div className={ENABLED_CLASS}>
        {enableAutomaticAdd ? (
          <Label kind={kinds.SUCCESS}>{translate('AutomaticAdd')}</Label>
        ) : null}
      </div>

      <TagList tags={tags} tagList={tagList} />

      <div className={ENABLED_CLASS}>
        <Label kind={kinds.DEFAULT} title="List Refresh Interval">
          {`${translate('Refresh')}: ${formatShortTimeSpan(
            minRefreshInterval
          )}`}
        </Label>
      </div>

      <EditImportListModal
        id={id}
        isOpen={isEditImportListModalOpen}
        onModalClose={handleEditImportListModalClose}
        onDeleteImportListPress={handleDeleteImportListPress}
      />

      <ConfirmModal
        isOpen={isDeleteImportListModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteImportList')}
        message={translate('DeleteImportListMessageText', { name })}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDeleteImportList}
        onCancel={handleDeleteImportListModalClose}
      />
    </Card>
  );
}

export default ImportList;
