import classNames from 'classnames';
import React, { useMemo } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import { CheckInputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import VirtualTableHeaderCell, {
  TABLE_HEADER_CELL_CLASS,
} from './TableHeaderCell';

interface TableSelectAllHeaderCellProps {
  allSelected: boolean;
  allUnselected: boolean;
  onSelectAllChange: (change: CheckInputChanged) => void;
}

function TableSelectAllHeaderCell({
  allSelected,
  allUnselected,
  onSelectAllChange,
}: TableSelectAllHeaderCellProps) {
  const value = useMemo(() => {
    if (allSelected) {
      return true;
    } else if (allUnselected) {
      return false;
    }

    return null;
  }, [allSelected, allUnselected]);

  return (
    <VirtualTableHeaderCell
      className={classNames(TABLE_HEADER_CELL_CLASS, 'w-[30px]')}
      name="selectAll"
    >
      <CheckInput
        className="m-0!"
        name="selectAll"
        ariaLabel={translate('SelectAll')}
        value={value}
        onChange={onSelectAllChange}
      />
    </VirtualTableHeaderCell>
  );
}

export default TableSelectAllHeaderCell;
