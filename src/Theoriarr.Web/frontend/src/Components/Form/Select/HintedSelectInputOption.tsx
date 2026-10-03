import classNames from 'classnames';
import React from 'react';
import EnhancedSelectInputOption, {
  EnhancedSelectInputOptionProps,
} from './EnhancedSelectInputOption';

interface HintedSelectInputOptionProps
  extends Omit<EnhancedSelectInputOptionProps, 'isSelected'> {
  value: string;
  hint?: React.ReactNode;
  isSelected?: boolean;
}

function HintedSelectInputOption(props: HintedSelectInputOptionProps) {
  const {
    id,
    value,
    hint,
    depth,
    isSelected = false,
    isMobile,
    ...otherProps
  } = props;

  return (
    <EnhancedSelectInputOption
      id={id}
      depth={depth}
      isSelected={isSelected}
      isMobile={isMobile}
      {...otherProps}
    >
      <div
        className={classNames(
          'flex min-w-0 flex-[1_0_0] items-center justify-between',
          isMobile && 'block'
        )}
      >
        <div>{value}</div>

        {hint != null && (
          <div
            className={classNames(
              'ml-[15px] max-w-full truncate text-[12px] text-[var(--darkGray)]',
              isMobile && 'ml-0'
            )}
          >
            {hint}
          </div>
        )}
      </div>
    </EnhancedSelectInputOption>
  );
}

export default HintedSelectInputOption;
