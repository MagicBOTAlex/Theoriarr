import classNames from 'classnames';
import React, { ReactNode } from 'react';
import Link, { LinkProps } from 'Components/Link/Link';
import TableRowCell from './TableRowCell';

const BUTTON_CLASS =
  "block -m-2! p-2 w-[calc(100%+16px)] font-[inherit] leading-[inherit] before:absolute before:inset-0 before:content-[''] focus-visible:before:outline-2 focus-visible:before:outline-[var(--linkColor)] focus-visible:before:[outline-offset:-2px]";

interface TableRowCellButtonProps extends LinkProps {
  className?: string;
  children: ReactNode;
}

function TableRowCellButton(props: TableRowCellButtonProps) {
  const { className, children, title, ...otherProps } = props;

  return (
    <TableRowCell className={classNames('relative', className)}>
      <Link
        className={BUTTON_CLASS}
        title={title}
        aria-label={title}
        {...otherProps}
      >
        {children}
      </Link>
    </TableRowCell>
  );
}

export default TableRowCellButton;
