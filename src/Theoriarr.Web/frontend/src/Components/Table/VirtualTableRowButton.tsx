import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';
import VirtualTableRow, { VIRTUAL_TABLE_ROW_CLASS } from './VirtualTableRow';

function VirtualTableRowButton(props: LinkProps) {
  return (
    <Link
      className={VIRTUAL_TABLE_ROW_CLASS}
      component={VirtualTableRow}
      {...props}
    />
  );
}

export default VirtualTableRowButton;
