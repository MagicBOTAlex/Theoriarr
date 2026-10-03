import classNames from 'classnames';
import React from 'react';
import { TABLE_ROW_CELL_CLASS } from './TableRowCell';

export const VIRTUAL_TABLE_ROW_CELL_CLASS = classNames(
  TABLE_ROW_CELL_CLASS,
  'overflow-hidden! max-w-full text-ellipsis! whitespace-nowrap!'
);

export interface VirtualTableRowCellProps {
  className?: string;
  children?: string | React.ReactNode;
}

function VirtualTableRowCell({
  className,
  children,
}: VirtualTableRowCellProps) {
  return (
    <div className={classNames(VIRTUAL_TABLE_ROW_CELL_CLASS, className)}>
      {children}
    </div>
  );
}

export default VirtualTableRowCell;
