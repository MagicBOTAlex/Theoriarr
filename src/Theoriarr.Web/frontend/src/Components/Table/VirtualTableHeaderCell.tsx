import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons, sortDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';

export const VIRTUAL_TABLE_HEADER_CELL_CLASS =
  'p-[8px] border-none! text-left font-bold max-[992px]:whitespace-nowrap';

export const VIRTUAL_SORT_ICON_CLASS = 'ml-[10px]';

interface VirtualTableHeaderCellProps {
  className?: string;
  name: string;
  isSortable?: boolean;
  sortKey?: string;
  fixedSortDirection?: SortDirection;
  sortDirection?: string;
  children?: React.ReactNode;
  onSortPress?: (name: string, sortDirection?: SortDirection) => void;
}

function VirtualTableHeaderCell({
  className = VIRTUAL_TABLE_HEADER_CELL_CLASS,
  name,
  isSortable = false,
  sortKey,
  sortDirection,
  fixedSortDirection,
  children,
  onSortPress,
  ...otherProps
}: VirtualTableHeaderCellProps) {
  const isSorting = isSortable && sortKey === name;
  const sortIcon =
    sortDirection === sortDirections.ASCENDING
      ? icons.SORT_ASCENDING
      : icons.SORT_DESCENDING;

  const handlePress = useCallback(() => {
    if (fixedSortDirection) {
      onSortPress?.(name, fixedSortDirection);
    } else {
      onSortPress?.(name);
    }
  }, [name, fixedSortDirection, onSortPress]);

  return isSortable ? (
    <Link
      component="div"
      className={className}
      onPress={handlePress}
      {...otherProps}
    >
      {children}

      {isSorting ? (
        <Icon name={sortIcon} className={VIRTUAL_SORT_ICON_CLASS} />
      ) : null}
    </Link>
  ) : (
    <div className={className}>{children}</div>
  );
}

export default VirtualTableHeaderCell;
