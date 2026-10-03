import React, { useCallback } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import MiddleTruncate from 'Components/MiddleTruncate';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { kinds } from 'Helpers/Props';
import { IndexerModel } from 'Settings/Indexers/useIndexers';
import { Tag } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import EditReleaseProfileModal from './EditReleaseProfileModal';
import {
  ReleaseProfileModel,
  useDeleteReleaseProfile,
} from './useReleaseProfiles';

const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';
const LABEL_CLASS =
  'inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default max-w-full';

interface ReleaseProfileProps extends ReleaseProfileModel {
  tagList: ReadonlyArray<Tag>;
  indexerList: ReadonlyArray<IndexerModel>;
}

function ReleaseProfileItem(props: ReleaseProfileProps) {
  const {
    id,
    name,
    enabled = true,
    required = [],
    ignored = [],
    indexerIds = [],
    tags,
    excludedTags,
    tagList,
    indexerList,
  } = props;

  const { deleteReleaseProfile } = useDeleteReleaseProfile(id);

  const [
    isEditReleaseProfileModalOpen,
    setEditReleaseProfileModalOpen,
    setEditReleaseProfileModalClosed,
  ] = useModalOpenState(false);

  const [
    isDeleteReleaseProfileModalOpen,
    setDeleteReleaseProfileModalOpen,
    setDeleteReleaseProfileModalClosed,
  ] = useModalOpenState(false);

  const handleDeletePress = useCallback(() => {
    deleteReleaseProfile();
  }, [deleteReleaseProfile]);

  const indexers = indexerList.filter((i) => indexerIds.includes(i.id));

  return (
    <Card
      className="w-[290px]"
      overlayContent={true}
      aria-label={translate('EditReleaseProfileName', { name: name ?? id })}
      onPress={setEditReleaseProfileModalOpen}
    >
      {name ? <div className={NAME_CLASS}>{name}</div> : null}

      <div>
        {required.map((item) => {
          if (!item) {
            return null;
          }

          return (
            <Label key={item} className={LABEL_CLASS} kind={kinds.SUCCESS}>
              <MiddleTruncate text={item} />
            </Label>
          );
        })}
      </div>

      <div>
        {ignored.map((item) => {
          if (!item) {
            return null;
          }

          return (
            <Label key={item} className={LABEL_CLASS} kind={kinds.DANGER}>
              <MiddleTruncate text={item} />
            </Label>
          );
        })}
      </div>

      <TagList tags={tags} tagList={tagList} />

      <TagList tags={excludedTags} tagList={tagList} kind={kinds.DANGER} />

      <div>
        {enabled ? null : (
          <Label kind={kinds.DISABLED} outline={true}>
            {translate('Disabled')}
          </Label>
        )}

        {indexers.map((indexer) => (
          <Label key={indexer.id} kind={kinds.INFO} outline={true}>
            {indexer.name}
          </Label>
        ))}
      </div>

      <EditReleaseProfileModal
        id={id}
        isOpen={isEditReleaseProfileModalOpen}
        onModalClose={setEditReleaseProfileModalClosed}
        onDeleteReleaseProfilePress={setDeleteReleaseProfileModalOpen}
      />

      <ConfirmModal
        isOpen={isDeleteReleaseProfileModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteReleaseProfile')}
        message={translate('DeleteReleaseProfileMessageText', {
          name: name ?? id,
        })}
        confirmLabel={translate('Delete')}
        onConfirm={handleDeletePress}
        onCancel={setDeleteReleaseProfileModalClosed}
      />
    </Card>
  );
}

export default ReleaseProfileItem;
