import classNames from 'classnames';
import React, { useMemo } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import { CheckInputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import VirtualTableHeaderCell, {
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
} from './VirtualTableHeaderCell';

interface VirtualTableSelectAllHeaderCellProps {
  allSelected: boolean;
  allUnselected: boolean;
  onSelectAllChange: (change: CheckInputChanged) => void;
}

function VirtualTableSelectAllHeaderCell({
  allSelected,
  allUnselected,
  onSelectAllChange,
}: VirtualTableSelectAllHeaderCellProps) {
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
      className={classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_36px]')}
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

export default VirtualTableSelectAllHeaderCell;
