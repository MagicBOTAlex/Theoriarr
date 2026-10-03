import React, { useCallback } from 'react';
import Label, { LabelProps } from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import MiddleTruncate from 'Components/MiddleTruncate';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { TagBase } from './TagInput';

export interface DeletedTag<T extends TagBase> {
  index: number;
  id: T['id'];
}

export interface EditedTag<T extends TagBase> {
  index: number;
  id: T['id'];
  value: T['name'];
}

export interface TagInputTagProps<T extends TagBase> {
  index: number;
  tag: T;
  kind: LabelProps['kind'];
  canEdit: boolean;
  onDelete: (deletedTag: DeletedTag<T>) => void;
  onEdit: (editedTag: EditedTag<T>) => void;
}

function TagInputTag<T extends TagBase>({
  tag,
  kind,
  index,
  canEdit,
  onDelete,
  onEdit,
}: TagInputTagProps<T>) {
  const handleDelete = useCallback(() => {
    onDelete({
      index,
      id: tag.id,
    });
  }, [index, tag, onDelete]);

  const handleEdit = useCallback(() => {
    onEdit({
      index,
      id: tag.id,
      value: tag.name,
    });
  }, [index, tag, onEdit]);

  return (
    <div
      className="flex justify-center flex-col max-w-full h-[31px]"
      tabIndex={-1}
    >
      <Label
        className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default flex! max-w-full"
        kind={kind}
      >
        <Link
          className={canEdit ? 'max-w-[calc(100%-9px-4px-2px)]' : 'max-w-full'}
          tabIndex={-1}
          onPress={handleDelete}
        >
          <MiddleTruncate text={String(tag.name)} />
        </Link>

        {canEdit ? (
          <div className="inline-block ml-[4px] pl-[2px] border-l border-solid border-l-[#eee]">
            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[9px]"
              name={icons.EDIT}
              aria-label={translate('Edit')}
              size={9}
              onPress={handleEdit}
            />
          </div>
        ) : null}
      </Label>
    </div>
  );
}

export default TagInputTag;
