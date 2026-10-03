import classNames from 'classnames';
import React, { useCallback } from 'react';
import { ConnectDragSource } from 'react-dnd';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import { ItemFailures } from './qualityProfileItemFailures';
import QualityProfileItemSize, { SizeChanged } from './QualityProfileItemSize';

const QUALITY_PROFILE_ITEM_CLASS =
  'flex items-stretch w-full border border-solid border-[#aaa] rounded-[4px] bg-[var(--inputBackgroundColor)]';
const QUALITY_NAME_CONTAINER_CLASS =
  'flex grow mb-0 ml-[2px] font-normal leading-[30px] cursor-pointer';
const DRAG_HANDLE_CLASS =
  'flex items-center justify-center shrink-0 ml-auto w-[40px] text-center cursor-grab';
const DRAG_ICON_CLASS = 'top-0';

interface QualityProfileItemProps {
  dragRef: ConnectDragSource;
  mode: string;
  isPreview?: boolean;
  groupId?: number;
  qualityId: number;
  name: string;
  allowed: boolean;
  minSize: number | null;
  maxSize: number | null;
  preferredSize: number | null;
  failures?: ItemFailures;
  isDragging: boolean;
  onCreateGroupPress?: (qualityId: number) => void;
  onItemAllowedChange: (qualityId: number, allowed: boolean) => void;
  onSizeChange: (change: SizeChanged) => void;
}

function QualityProfileItem({
  dragRef,
  mode = 'default',
  isPreview = false,
  qualityId,
  groupId,
  name,
  allowed,
  minSize,
  maxSize,
  isDragging,
  preferredSize,
  failures,
  onCreateGroupPress,
  onItemAllowedChange,
  onSizeChange,
}: QualityProfileItemProps) {
  const handleAllowedChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      onItemAllowedChange?.(qualityId, value);
    },
    [qualityId, onItemAllowedChange]
  );

  const handleCreateGroupPress = useCallback(() => {
    onCreateGroupPress?.(qualityId);
  }, [qualityId, onCreateGroupPress]);

  return (
    <div
      className={classNames(
        QUALITY_PROFILE_ITEM_CLASS,
        mode === 'editSizes' && 'flex-col p-[10px]',
        isDragging && 'opacity-25',
        groupId && 'border-dashed!'
      )}
    >
      <label
        className={classNames(
          QUALITY_NAME_CONTAINER_CLASS,
          mode === 'editSizes' && 'cursor-default!'
        )}
      >
        {mode === 'editGroups' && !groupId && !isPreview && (
          <IconButton
            className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] flex! items-center justify-center shrink-0 mr-[5px] ml-2 w-5"
            name={icons.GROUP}
            title={translate('Group')}
            aria-label={translate('Group')}
            onPress={handleCreateGroupPress}
          />
        )}

        {mode === 'default' && (
          <CheckInput
            className="mt-[5px]!"
            containerClassName="relative mr-[4px] mb-[5px] ml-[8px]"
            name={name}
            value={allowed}
            isDisabled={!!groupId}
            onChange={handleAllowedChange}
          />
        )}

        <div
          className={classNames(
            groupId && mode !== 'editSizes' && 'ml-[14px]',
            !allowed && 'text-[#c6c6c6]',
            isPreview && 'ml-[14px]',
            isPreview && groupId && mode !== 'editSizes' && 'ml-[28px]'
          )}
        >
          {name}
        </div>
      </label>

      {mode === 'editSizes' && qualityId != null ? (
        <div>
          <QualityProfileItemSize
            id={qualityId}
            minSize={minSize}
            maxSize={maxSize}
            preferredSize={preferredSize}
            failures={failures}
            onSizeChange={onSizeChange}
          />
        </div>
      ) : null}

      {mode === 'editSizes' ? null : (
        <div ref={dragRef} className={DRAG_HANDLE_CLASS}>
          <Icon
            className={DRAG_ICON_CLASS}
            title={translate('CreateGroup')}
            name={icons.REORDER}
          />
        </div>
      )}
    </div>
  );
}

export default QualityProfileItem;
