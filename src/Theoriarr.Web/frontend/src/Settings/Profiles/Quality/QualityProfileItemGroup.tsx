import classNames from 'classnames';
import React, { useCallback } from 'react';
import { ConnectDragSource } from 'react-dnd';
import CheckInput from 'Components/Form/CheckInput';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import QualityProfileItemDragSource, {
  DragMoveState,
} from './QualityProfileItemDragSource';
import { getItemFailures, ItemFailuresMap } from './qualityProfileItemFailures';
import { SizeChanged } from './QualityProfileItemSize';
import { QualityProfileQualityItem } from './useQualityProfiles';

const NAME_INPUT_CLASS = classNames(INPUT_CLASS, 'mt-[4px] mr-[10px]');
const QUALITY_NAME_CONTAINER_CLASS =
  'flex items-stretch grow mb-0 ml-[2px] font-normal';

interface QualityProfileItemGroupProps {
  dragRef: ConnectDragSource;
  mode?: string;
  groupId: number;
  name: string;
  allowed: boolean;
  items: QualityProfileQualityItem[];
  qualityIndex: string;
  itemFailures?: ItemFailuresMap;
  isDragging: boolean;
  isDraggingUp: boolean;
  isDraggingDown: boolean;
  onGroupAllowedChange: (groupId: number, allowed: boolean) => void;
  onItemAllowedChange: (groupId: number, allowed: boolean) => void;
  onItemGroupNameChange: (groupId: number, name: string) => void;
  onDeleteGroupPress: (groupId: number) => void;
  onDragMove: (drag: DragMoveState) => void;
  onDragEnd: (didDrop: boolean) => void;
  onSizeChange: (sizeChange: SizeChanged) => void;
}

function QualityProfileItemGroup({
  dragRef,
  mode = 'default',
  groupId,
  name,
  allowed,
  items,
  qualityIndex,
  itemFailures,
  isDragging,
  isDraggingUp,
  isDraggingDown,
  onDeleteGroupPress,
  onGroupAllowedChange,
  onItemAllowedChange,
  onItemGroupNameChange,
  onDragMove,
  onDragEnd,
  onSizeChange,
}: QualityProfileItemGroupProps) {
  const groupBaseIndex = parseInt(qualityIndex) - 1;
  const handleAllowedChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      onGroupAllowedChange?.(groupId, value);
    },
    [groupId, onGroupAllowedChange]
  );

  const handleNameChange = useCallback(
    ({ value }: InputChanged<string>) => {
      onItemGroupNameChange?.(groupId, value);
    },
    [groupId, onItemGroupNameChange]
  );

  const handleDeleteGroupPress = useCallback(() => {
    onDeleteGroupPress?.(groupId);
  }, [groupId, onDeleteGroupPress]);

  return (
    <div
      className={classNames(
        'w-full border border-solid border-[#aaa] rounded-[4px] bg-[var(--inputBackgroundColor)]',
        mode === 'editSizes' && 'p-[10px]',
        isDragging && 'opacity-25'
      )}
    >
      <div className="flex items-stretch w-full">
        {mode === 'editGroups' ? (
          <div className={QUALITY_NAME_CONTAINER_CLASS}>
            <IconButton
              className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] flex! items-center justify-center shrink-0 mr-[5px] ml-2 w-5"
              name={icons.UNGROUP}
              title={translate('Ungroup')}
              aria-label={translate('Ungroup')}
              onPress={handleDeleteGroupPress}
            />

            <TextInput
              className={NAME_INPUT_CLASS}
              name="name"
              value={name}
              onChange={handleNameChange}
            />
          </div>
        ) : null}

        {mode === 'default' ? (
          <label
            className={classNames(
              QUALITY_NAME_CONTAINER_CLASS,
              'cursor-pointer'
            )}
          >
            <CheckInput
              className="mt-[5px]!"
              containerClassName="relative mr-[4px] mb-[5px] ml-[8px] flex items-center"
              name="allowed"
              value={allowed}
              onChange={handleAllowedChange}
            />

            <div className="flex items-center grow">
              <div
                className={classNames('shrink-0', !allowed && 'text-[#c6c6c6]')}
              >
                {name}
              </div>

              <div className="flex justify-end grow flex-wrap my-[2px] ml-[10px]">
                {items
                  .map(({ quality }) => {
                    return <Label key={quality.id}>{quality.name}</Label>;
                  })
                  .reverse()}
              </div>
            </div>
          </label>
        ) : null}

        {mode === 'editSizes' ? (
          <label className={QUALITY_NAME_CONTAINER_CLASS}>
            <div className="flex items-center grow">
              <div
                className={classNames('shrink-0', !allowed && 'text-[#c6c6c6]')}
              >
                {name}
              </div>
            </div>
          </label>
        ) : null}

        {mode === 'editSizes' ? null : (
          <div
            ref={dragRef}
            className="flex items-center justify-center shrink-0 ml-auto w-[40px] text-center cursor-grab"
          >
            <Icon
              className="top-0"
              name={icons.REORDER}
              title={translate('Reorder')}
            />
          </div>
        )}
      </div>

      {mode === 'default' ? null : (
        <div
          className={mode === 'editGroups' ? 'mr-[50px] ml-[35px]' : undefined}
        >
          {items
            .map((subItem, index) => {
              const { quality, minSize, maxSize, preferredSize } = subItem;

              return (
                <QualityProfileItemDragSource
                  key={quality.id}
                  mode={mode}
                  groupId={groupId}
                  qualityId={quality.id}
                  name={quality.name}
                  allowed={allowed}
                  minSize={minSize}
                  maxSize={maxSize}
                  preferredSize={preferredSize}
                  failures={
                    itemFailures
                      ? getItemFailures(itemFailures, groupBaseIndex, index)
                      : undefined
                  }
                  qualityIndex={`${qualityIndex}.${index + 1}`}
                  isDraggingUp={isDraggingUp}
                  isDraggingDown={isDraggingDown}
                  isInGroup={true}
                  onItemAllowedChange={onItemAllowedChange}
                  onDragMove={onDragMove}
                  onDragEnd={onDragEnd}
                  onSizeChange={onSizeChange}
                />
              );
            })
            .reverse()}
        </div>
      )}
    </div>
  );
}

export default QualityProfileItemGroup;
