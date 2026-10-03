import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';
import TableRow, { TABLE_ROW_CLASS } from './TableRow';

function TableRowButton(props: LinkProps) {
  return <Link className={TABLE_ROW_CLASS} component={TableRow} {...props} />;
}

export default TableRowButton;
