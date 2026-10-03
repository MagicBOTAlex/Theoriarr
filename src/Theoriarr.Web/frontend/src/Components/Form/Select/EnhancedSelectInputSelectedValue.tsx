import classNames from 'classnames';
import React, { ReactNode } from 'react';

const SELECTED_VALUE_CLASS = 'flex-[1_1_auto]';

const DISABLED_CLASS = 'text-[var(--disabledInputColor)]';

interface EnhancedSelectInputSelectedValueProps {
  className?: string;
  children: ReactNode;
  isDisabled?: boolean;
}

function EnhancedSelectInputSelectedValue({
  className = SELECTED_VALUE_CLASS,
  children,
  isDisabled = false,
}: EnhancedSelectInputSelectedValueProps) {
  return (
    <div className={classNames(className, isDisabled && DISABLED_CLASS)}>
      {children}
    </div>
  );
}

export default EnhancedSelectInputSelectedValue;
