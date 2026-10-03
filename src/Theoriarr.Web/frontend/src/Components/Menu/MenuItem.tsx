import classNames from 'classnames';
import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';

const MENU_ITEM_CLASS =
  'block min-w-[150px] max-w-[250px] shrink-0 truncate bg-[var(--toolbarMenuItemBackgroundColor)] px-5 py-2.5 leading-[20px] text-[var(--menuItemColor)] hover:bg-[var(--toolbarMenuItemHoverBackgroundColor)] hover:text-[var(--menuItemHoverColor)] hover:no-underline focus:bg-[var(--toolbarMenuItemHoverBackgroundColor)] focus:text-[var(--menuItemHoverColor)] focus:no-underline';

const DISABLED_CLASS = 'pointer-events-none text-[var(--disabledColor)]';

export interface MenuItemProps extends LinkProps {
  className?: string;
  children: React.ReactNode;
  isDisabled?: boolean;
}

function MenuItem({
  className = MENU_ITEM_CLASS,
  children,
  isDisabled = false,
  ...otherProps
}: MenuItemProps) {
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

export default MenuItem;
