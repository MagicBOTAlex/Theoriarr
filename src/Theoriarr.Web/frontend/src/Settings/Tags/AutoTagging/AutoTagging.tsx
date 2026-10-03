import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import { icons, kinds } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import { Tag } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import EditAutoTaggingModal from './EditAutoTaggingModal';
import {
  AutoTaggingSpecification,
  useDeleteAutoTagging,
} from './useAutoTaggings';

const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';

interface AutoTaggingProps {
  id: number;
  name: string;
  specifications: AutoTaggingSpecification[];
  tags: number[];
  tagList: ReadonlyArray<Tag>;
  onCloneAutoTaggingPress: (id: number) => void;
}

export default function AutoTagging({
  id,
  name,
  tags,
  tagList,
  specifications,
  onCloneAutoTaggingPress,
}: AutoTaggingProps) {
  const { deleteAutoTagging, isDeleting } = useDeleteAutoTagging(id);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);

  const onEditPress = useCallback(() => {
    setIsEditModalOpen(true);
  }, [setIsEditModalOpen]);

  const onEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
  }, [setIsEditModalOpen]);

  const onDeletePress = useCallback(() => {
    setIsEditModalOpen(false);
    setIsDeleteModalOpen(true);
  }, [setIsEditModalOpen, setIsDeleteModalOpen]);

  const onDeleteModalClose = useCallback(() => {
    setIsDeleteModalOpen(false);
  }, [setIsDeleteModalOpen]);

  const onConfirmDelete = useCallback(() => {
    deleteAutoTagging();
  }, [deleteAutoTagging]);

  const onClonePress = useCallback(() => {
    onCloneAutoTaggingPress(id);
  }, [id, onCloneAutoTaggingPress]);

  return (
    <Card
      className="w-[300px]"
      overlayContent={true}
      aria-label={translate('EditAutoTagName', { name })}
      onPress={onEditPress}
    >
      <div className="flex justify-between">
        <div className={NAME_CLASS}>{name}</div>

        <div>
          <IconButton
            className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] h-[36px]"
            title={translate('CloneAutoTag')}
            aria-label={translate('CloneAutoTag')}
            name={icons.CLONE}
            onPress={onClonePress}
          />
        </div>
      </div>

      <TagList tags={tags} tagList={tagList} />

      <div>
        {specifications.map((item, index) => {
          if (!item) {
            return null;
          }

          let kind: Kind = 'default';

          if (item.required) {
            kind = 'success';
          }

          if (item.negate) {
            kind = 'danger';
          }

          return (
            <Label key={index} kind={kind}>
              {item.name}
            </Label>
          );
        })}
      </div>

      <EditAutoTaggingModal
        id={id}
        isOpen={isEditModalOpen}
        onModalClose={onEditModalClose}
        onDeleteAutoTaggingPress={onDeletePress}
      />

      <ConfirmModal
        isOpen={isDeleteModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteAutoTag')}
        message={translate('DeleteAutoTagHelpText', { name })}
        confirmLabel={translate('Delete')}
        isSpinning={isDeleting}
        onConfirm={onConfirmDelete}
        onCancel={onDeleteModalClose}
      />
    </Card>
  );
}
