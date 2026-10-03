import classNames from 'classnames';
import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import MenuButton, { MenuButtonProps } from 'Components/Menu/MenuButton';
import { icons } from 'Helpers/Props';

export const TOOLBAR_MENU_BUTTON_CLASS =
  "flex h-15 w-15 items-center justify-center pt-1 text-center after:ml-[5px] after:content-['▾'] hover:text-[var(--toobarButtonHoverColor)]";

const INDICATOR_CONTAINER_CLASS = 'absolute top-2.5 right-3';

const LABEL_CONTAINER_CLASS =
  'flex h-6 items-center justify-center overflow-hidden';

const LABEL_CLASS =
  'max-h-full max-w-full px-[3px] text-[11px] leading-[12px] text-[var(--toolbarLabelColor)]';

export interface ToolbarMenuButtonProps
  extends Omit<MenuButtonProps, 'children'> {
  className?: string;
  iconName: IconName;
  showIndicator?: boolean;
  text?: string;
}

function ToolbarMenuButton({
  iconName,
  showIndicator = false,
  text,
  ...otherProps
}: ToolbarMenuButtonProps) {
  return (
    <MenuButton className={TOOLBAR_MENU_BUTTON_CLASS} {...otherProps}>
      <div>
        <Icon name={iconName} size={21} />

        {showIndicator ? (
          <span
            className={classNames(INDICATOR_CONTAINER_CLASS, 'fa-layers fa-fw')}
          >
            <Icon name={icons.CIRCLE} size={9} />
          </span>
        ) : null}

        <div className={LABEL_CONTAINER_CLASS}>
          <div className={LABEL_CLASS}>{text}</div>
        </div>
      </div>
    </MenuButton>
  );
}

export default ToolbarMenuButton;
