import classNames from 'classnames';
import React, { SyntheticEvent, useCallback } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import CheckInput from '../CheckInput';

export const ENHANCED_SELECT_INPUT_OPTION_CLASS =
  'flex items-center justify-between px-[10px] py-[5px] w-full cursor-default hover:bg-[var(--inputHoverBackgroundColor)]';

function handleCheckPress() {
  // CheckInput requires a handler. Swallow the change event because onPress will already handle it via event propagation.
}

export interface EnhancedSelectInputOptionProps {
  className?: string;
  id: string | number;
  depth?: number;
  isSelected: boolean;
  isDisabled?: boolean;
  isHidden?: boolean;
  isMultiSelect?: boolean;
  isMobile: boolean;
  children: React.ReactNode;
  onSelect: (...args: unknown[]) => unknown;
}

function EnhancedSelectInputOption({
  className = ENHANCED_SELECT_INPUT_OPTION_CLASS,
  id,
  depth = 0,
  isSelected,
  isDisabled = false,
  isHidden = false,
  isMultiSelect = false,
  isMobile,
  children,
  onSelect,
}: EnhancedSelectInputOptionProps) {
  const handlePress = useCallback(
    (event: SyntheticEvent) => {
      event.preventDefault();

      onSelect(id);
    },
    [id, onSelect]
  );

  return (
    <Link
      className={classNames(
        className,
        isSelected &&
          !isMultiSelect &&
          'bg-[var(--inputSelectedBackgroundColor)] hover:bg-[var(--inputSelectedBackgroundColor)]',
        isDisabled && !isMultiSelect && 'cursor-not-allowed bg-[#aaa]',
        isHidden && 'hidden',
        isMobile &&
          'h-[50px] border-b border-solid border-[var(--borderColor)] last:border-none hover:bg-transparent',
        isSelected && isMobile && 'bg-inherit'
      )}
      component="div"
      isDisabled={isDisabled}
      onPress={handlePress}
    >
      {depth !== 0 && <div style={{ width: `${depth * 20}px` }} />}

      {isMultiSelect && (
        <CheckInput
          className="mt-0!"
          containerClassName="relative flex flex-[1_1_65%] select-none flex-[0_0_0]!"
          name={`select-${id}`}
          value={isSelected}
          isDisabled={isDisabled}
          onChange={handleCheckPress}
        />
      )}

      {children}

      {isMobile && (
        <div
          className={
            isSelected && isMobile ? 'text-[var(--primaryColor)]' : undefined
          }
        >
          <Icon name={isSelected ? icons.CHECK_CIRCLE : icons.CIRCLE_OUTLINE} />
        </div>
      )}
    </Link>
  );
}

export default EnhancedSelectInputOption;
