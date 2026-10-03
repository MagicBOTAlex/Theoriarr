import React from 'react';

export const VIRTUAL_TABLE_ROW_CLASS =
  'flex transition-[background-color] duration-500 hover:bg-[var(--tableRowHoverBackgroundColor)] max-[992px]:overflow-x-visible!';

interface VirtualTableRowProps extends React.HTMLAttributes<HTMLDivElement> {
  className?: string;
  style: object;
  children?: React.ReactNode;
}

function VirtualTableRow({
  className = VIRTUAL_TABLE_ROW_CLASS,
  children,
  style,
  ...otherProps
}: VirtualTableRowProps) {
  return (
    <div className={className} style={style} {...otherProps}>
      {children}
    </div>
  );
}

export default VirtualTableRow;
