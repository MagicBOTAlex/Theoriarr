import classNames from 'classnames';
import React, { ComponentPropsWithoutRef } from 'react';

export const TABLE_ROW_CELL_CLASS =
  'p-2 border-t border-solid border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] leading-[1.52857143] max-[992px]:whitespace-nowrap';

export type TableRowCellProps = ComponentPropsWithoutRef<'td'>;

export default function TableRowCell({
  className,
  ...tdProps
}: TableRowCellProps) {
  return (
    <td className={classNames(TABLE_ROW_CELL_CLASS, className)} {...tdProps} />
  );
}
