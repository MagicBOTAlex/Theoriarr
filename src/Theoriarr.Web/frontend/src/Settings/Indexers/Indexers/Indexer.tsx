import React, { useCallback, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import { icons, kinds } from 'Helpers/Props';
import { useTagList } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import { IndexerModel, useDeleteIndexer } from '../useIndexers';
import EditIndexerModal from './EditIndexerModal';

const NAME_CONTAINER_CLASS = 'flex justify-between';
const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';

interface IndexerProps extends IndexerModel {
  showPriority: boolean;
  onCloneIndexerPress: (id: number) => void;
}

function Indexer({
  id,
  name,
  protocol,
  enableRss,
  enableAutomaticSearch,
  enableInteractiveSearch,
  tags,
  supportsRss,
  supportsSearch,
  priority,
  showPriority,
  onCloneIndexerPress,
}: IndexerProps) {
  const tagList = useTagList();
  const { deleteIndexer } = useDeleteIndexer(id);

  const [isEditIndexerModalOpen, setIsEditIndexerModalOpen] = useState(false);
  const [isDeleteIndexerModalOpen, setIsDeleteIndexerModalOpen] =
    useState(false);

  const handleEditIndexerPress = useCallback(() => {
    setIsEditIndexerModalOpen(true);
  }, []);

  const handleEditIndexerModalClose = useCallback(() => {
    setIsEditIndexerModalOpen(false);
  }, []);

  const handleDeleteIndexerPress = useCallback(() => {
    setIsEditIndexerModalOpen(false);
    setIsDeleteIndexerModalOpen(true);
  }, []);

  const handleDeleteIndexerModalClose = useCallback(() => {
    setIsDeleteIndexerModalOpen(false);
  }, []);

  const handleConfirmDeleteIndexer = useCallback(() => {
    deleteIndexer();
  }, [deleteIndexer]);

  const handleCloneIndexerPress = useCallback(() => {
    onCloneIndexerPress(id);
  }, [id, onCloneIndexerPress]);

  return (
    <Card
      className="w-[290px]"
      overlayContent={true}
      aria-label={translate('EditIndexerName', { name })}
      onPress={handleEditIndexerPress}
    >
      <div className={NAME_CONTAINER_CLASS}>
        <div className={NAME_CLASS}>{name}</div>

        <IconButton
          className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] h-[36px]"
          title={translate('CloneIndexer')}
          aria-label={translate('CloneIndexer')}
          name={icons.CLONE}
          onPress={handleCloneIndexerPress}
        />
      </div>

      <div className="flex flex-wrap mt-[5px]">
        <ProtocolLabel protocol={protocol} />

        {supportsRss && enableRss ? (
          <Label kind={kinds.SUCCESS}>{translate('Rss')}</Label>
        ) : null}

        {supportsSearch && enableAutomaticSearch ? (
          <Label kind={kinds.SUCCESS}>{translate('AutomaticSearch')}</Label>
        ) : null}

        {supportsSearch && enableInteractiveSearch ? (
          <Label kind={kinds.SUCCESS}>{translate('InteractiveSearch')}</Label>
        ) : null}

        {showPriority ? (
          <Label kind={kinds.DEFAULT}>
            {translate('Priority')}: {priority}
          </Label>
        ) : null}

        {!enableRss && !enableAutomaticSearch && !enableInteractiveSearch ? (
          <Label kind={kinds.DISABLED} outline={true}>
            {translate('Disabled')}
          </Label>
        ) : null}
      </div>

      <TagList tags={tags} tagList={tagList} />

      <EditIndexerModal
        id={id}
        isOpen={isEditIndexerModalOpen}
        onModalClose={handleEditIndexerModalClose}
        onDeleteIndexerPress={handleDeleteIndexerPress}
      />

      <ConfirmModal
        isOpen={isDeleteIndexerModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteIndexer')}
        message={translate('DeleteIndexerMessageText', { name })}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDeleteIndexer}
        onCancel={handleDeleteIndexerModalClose}
      />
    </Card>
  );
}

export default Indexer;
