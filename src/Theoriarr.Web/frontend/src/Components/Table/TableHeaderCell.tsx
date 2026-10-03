import React, { useCallback, useMemo } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons, sortDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { IsModifiable } from './Column';

export const TABLE_HEADER_CELL_CLASS =
  'p-[8px] border-none! text-left font-bold max-[992px]:whitespace-nowrap';

export const SORT_ICON_CLASS = 'ml-[10px]';

interface TableHeaderCellProps {
  className?: string;
  name: string;
  label?: string | (() => string) | React.ReactNode;
  columnLabel?: string | (() => string);
  isSortable?: boolean;
  isVisible?: boolean;
  isModifiable?: IsModifiable;
  sortKey?: string;
  fixedSortDirection?: SortDirection;
  sortDirection?: string;
  children?: React.ReactNode;
  onSortPress?: (name: string, sortDirection?: SortDirection) => void;
}

function TableHeaderCell({
  className = TABLE_HEADER_CELL_CLASS,
  name,
  label,
  columnLabel,
  isSortable = false,
  isVisible,
  isModifiable,
  sortKey,
  sortDirection,
  fixedSortDirection,
  children,
  onSortPress,
  ...otherProps
}: TableHeaderCellProps) {
  const isSorting = isSortable && sortKey === name;
  const sortIcon =
    sortDirection === sortDirections.ASCENDING
      ? icons.SORT_ASCENDING
      : icons.SORT_DESCENDING;

  const ariaSortValue = useMemo(() => {
    if (!isSortable) {
      return undefined;
    }

    if (!isSorting) {
      return 'none';
    }

    return sortDirection === sortDirections.ASCENDING
      ? 'ascending'
      : 'descending';
  }, [isSorting, sortDirection, isSortable]);

  const handlePress = useCallback(() => {
    if (fixedSortDirection) {
      onSortPress?.(name, fixedSortDirection);
    } else {
      onSortPress?.(name);
    }
  }, [name, fixedSortDirection, onSortPress]);

  return isSortable ? (
    <Link
      {...otherProps}
      component="th"
      className={className}
      // label={typeof label === 'function' ? label() : label}
      title={typeof columnLabel === 'function' ? columnLabel() : columnLabel}
      scope="col"
      aria-sort={ariaSortValue}
      onPress={handlePress}
    >
      {children}

      {isSorting ? (
        <Icon name={sortIcon} className={SORT_ICON_CLASS} aria-hidden={true} />
      ) : null}
    </Link>
  ) : (
    <th className={className} scope="col">
      {children}
    </th>
  );
}

export default TableHeaderCell;
