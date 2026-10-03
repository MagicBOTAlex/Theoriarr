import React from 'react';

export const TABLE_ROW_CLASS =
  'transition-[background-color] duration-500 hover:bg-[var(--tableRowHoverBackgroundColor)]';

interface TableRowProps extends React.HTMLAttributes<HTMLTableRowElement> {
  className?: string;
  children?: React.ReactNode;
  overlayContent?: boolean;
}

function TableRow({
  className = TABLE_ROW_CLASS,
  children,
  overlayContent,
  ...otherProps
}: TableRowProps) {
  return (
    <tr className={className} {...otherProps}>
      {children}
    </tr>
  );
}

export default TableRow;
