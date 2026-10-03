import React, { useCallback } from 'react';
import { SetFilter } from 'Components/Filter/Filter';
import FilterModal, { FilterModalProps } from 'Components/Filter/FilterModal';
import { FilterBuilderProp } from 'Filters/Filter';
import {
  BLOCKLIST_FILTER_BUILDER,
  HISTORY_FILTER_BUILDER,
  QUEUE_FILTER_BUILDER,
} from './activityFilters';
import {
  setBlocklistOption,
  setHistoryOption,
  setQueueOption,
} from './activityOptionsStore';
import {
  MediaBlocklistItem,
  MediaHistoryItem,
  MediaQueueItem,
} from './mediaActivity';

export type ActivityTab = 'queue' | 'history' | 'blocklist';

type ActivityItem = MediaQueueItem | MediaHistoryItem | MediaBlocklistItem;

interface ActivityFilterModalProps
  extends Omit<
    FilterModalProps<unknown>,
    | 'sectionItems'
    | 'filterBuilderProps'
    | 'customFilterType'
    | 'dispatchSetFilter'
  > {
  type: ActivityTab;
  items: ReadonlyArray<ActivityItem>;
}

function getConfig(type: ActivityTab) {
  switch (type) {
    case 'history':
      return {
        customFilterType: 'activity-history',
        filterBuilderProps:
          HISTORY_FILTER_BUILDER as unknown as FilterBuilderProp<unknown>[],
      };
    case 'blocklist':
      return {
        customFilterType: 'activity-blocklist',
        filterBuilderProps:
          BLOCKLIST_FILTER_BUILDER as unknown as FilterBuilderProp<unknown>[],
      };
    default:
      return {
        customFilterType: 'activity-queue',
        filterBuilderProps:
          QUEUE_FILTER_BUILDER as unknown as FilterBuilderProp<unknown>[],
      };
  }
}

export default function ActivityFilterModal({
  type,
  items,
  ...props
}: ActivityFilterModalProps) {
  const { customFilterType, filterBuilderProps } = getConfig(type);

  const dispatchSetFilter = useCallback(
    ({ selectedFilterKey }: SetFilter) => {
      if (type === 'queue') {
        setQueueOption('selectedFilterKey', selectedFilterKey);
      } else if (type === 'history') {
        setHistoryOption('selectedFilterKey', selectedFilterKey);
      } else {
        setBlocklistOption('selectedFilterKey', selectedFilterKey);
      }
    },
    [type]
  );

  return (
    <FilterModal
      {...props}
      sectionItems={items}
      filterBuilderProps={filterBuilderProps}
      customFilterType={customFilterType}
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
