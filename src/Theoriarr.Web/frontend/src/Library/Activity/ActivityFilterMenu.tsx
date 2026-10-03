import classNames from 'classnames';
import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import FilterMenu from 'Components/Menu/FilterMenu';
import MenuButton from 'Components/Menu/MenuButton';
import { CustomFilter, Filter } from 'Filters/Filter';
import ActivityFilterModal, { ActivityTab } from './ActivityFilterModal';
import {
  MediaBlocklistItem,
  MediaHistoryItem,
  MediaQueueItem,
} from './mediaActivity';

type ActivityItem = MediaQueueItem | MediaHistoryItem | MediaBlocklistItem;

interface ActivityFilterButtonProps {
  iconName: IconName;
  showIndicator?: boolean;
  text?: string;
  isDisabled?: boolean;
  onPress?: () => void;
}

function ActivityFilterButton({
  iconName,
  showIndicator = false,
  text,
  isDisabled,
  onPress,
}: ActivityFilterButtonProps) {
  return (
    <MenuButton
      className={classNames(
        'inline-flex h-[34px] items-center gap-[7px] rounded-[8px] border px-[12px] text-[13px] font-medium transition-colors',
        'border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] text-[var(--textColor)]',
        'enabled:hover:border-[var(--themeBlue)] enabled:hover:text-[var(--themeBlue)]',
        isDisabled && 'pointer-events-none opacity-50'
      )}
      isDisabled={isDisabled}
      onPress={onPress}
    >
      <Icon name={iconName} size={13} />

      <span>{text}</span>

      {showIndicator ? (
        <span className="h-[7px] w-[7px] rounded-full bg-[var(--themeBlue)]" />
      ) : null}
    </MenuButton>
  );
}

interface ActivityFilterMenuProps {
  type: ActivityTab;
  items: ReadonlyArray<ActivityItem>;
  selectedFilterKey: string | number;
  filters: Filter[];
  customFilters: CustomFilter[];
  onFilterSelect: (filter: number | string) => void;
}

function ActivityFilterMenu({
  type,
  items,
  selectedFilterKey,
  filters,
  customFilters,
  onFilterSelect,
}: ActivityFilterMenuProps) {
  return (
    <FilterMenu
      alignMenu="right"
      buttonComponent={ActivityFilterButton}
      selectedFilterKey={selectedFilterKey}
      filters={filters}
      customFilters={customFilters}
      filterModalConnectorComponent={ActivityFilterModal}
      filterModalConnectorComponentProps={{ type, items }}
      onFilterSelect={onFilterSelect}
    />
  );
}

export default ActivityFilterMenu;
