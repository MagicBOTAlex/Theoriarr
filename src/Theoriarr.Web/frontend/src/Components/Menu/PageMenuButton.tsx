import { IconName } from '@fortawesome/free-regular-svg-icons';
import classNames from 'classnames';
import React from 'react';
import Icon from 'Components/Icon';
import MenuButton from 'Components/Menu/MenuButton';
import { icons } from 'Helpers/Props';

const PAGE_MENU_BUTTON_CLASS =
  "relative flex h-full items-center justify-center after:ml-[5px] after:content-['▾'] hover:text-[#666]";

const INDICATOR_CONTAINER_CLASS = 'absolute top-2.5 left-2.5';

const LABEL_CLASS = 'ml-[5px]';

interface PageMenuButtonProps {
  iconName: IconName;
  showIndicator: boolean;
  text?: string;
}

function PageMenuButton({
  iconName,
  showIndicator = false,
  text,
  ...otherProps
}: PageMenuButtonProps) {
  return (
    <MenuButton className={PAGE_MENU_BUTTON_CLASS} {...otherProps}>
      <Icon name={iconName} size={18} />

      {showIndicator ? (
        <span
          className={classNames(INDICATOR_CONTAINER_CLASS, 'fa-layers fa-fw')}
        >
          <Icon name={icons.CIRCLE} size={9} />
        </span>
      ) : null}

      <div className={LABEL_CLASS}>{text}</div>
    </MenuButton>
  );
}

export default PageMenuButton;
