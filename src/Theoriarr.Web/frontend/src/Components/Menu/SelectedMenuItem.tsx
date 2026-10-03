import React, { useCallback } from 'react';
import Icon, { IconName } from 'Components/Icon';
import { icons } from 'Helpers/Props';
import MenuItem, { MenuItemProps } from './MenuItem';

export interface SelectedMenuItemProps extends Omit<MenuItemProps, 'onPress'> {
  name?: string;
  children: React.ReactNode;
  selectedIconName?: IconName;
  isSelected: boolean;
  onPress: (name: string) => void;
}

function SelectedMenuItem({
  children,
  name,
  selectedIconName = icons.CHECK,
  isSelected,
  onPress,
  ...otherProps
}: SelectedMenuItemProps) {
  const handlePress = useCallback(() => {
    onPress(name ?? '');
  }, [name, onPress]);

  return (
    <MenuItem {...otherProps} onPress={handlePress}>
      <div className="flex justify-between whitespace-nowrap">
        {children}

        <Icon
          className={isSelected ? 'visible ml-5' : 'invisible ml-5'}
          name={selectedIconName}
        />
      </div>
    </MenuItem>
  );
}

export default SelectedMenuItem;
