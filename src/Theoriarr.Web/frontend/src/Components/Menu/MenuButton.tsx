import classNames from 'classnames';
import React from 'react';
import Link from 'Components/Link/Link';

const MENU_BUTTON_CLASS =
  "flex h-full items-center justify-center after:ml-[5px] after:content-['▾'] hover:text-[var(--toobarButtonHoverColor)]";

const DISABLED_CLASS = 'pointer-events-none text-[var(--disabledColor)]';

export interface MenuButtonProps {
  className?: string;
  children: React.ReactNode;
  isDisabled?: boolean;
  onPress?: () => void;
}

function MenuButton({
  className = MENU_BUTTON_CLASS,
  children,
  isDisabled = false,
  ...otherProps
}: MenuButtonProps) {
  return (
    <Link
      className={classNames(className, isDisabled && DISABLED_CLASS)}
      isDisabled={isDisabled}
      {...otherProps}
    >
      {children}
    </Link>
  );
}

export default MenuButton;
