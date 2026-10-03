import classNames from 'classnames';
import React, { useCallback, useEffect, useRef } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import { CheckInputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import TableRowCell, { TableRowCellProps } from './TableRowCell';

interface TableSelectCellProps<T extends number | string = number>
  extends Omit<TableRowCellProps, 'id'> {
  className?: string;
  id: T;
  isSelected?: boolean;
  onSelectedChange: (options: SelectStateInputProps<T>) => void;
}

function TableSelectCell<T extends number | string = number>({
  className,
  id,
  isSelected = false,
  onSelectedChange,
  ...otherProps
}: TableSelectCellProps<T>) {
  const initialIsSelected = useRef(isSelected);
  const handleSelectedChange = useRef(onSelectedChange);

  handleSelectedChange.current = onSelectedChange;

  const handleChange = useCallback(
    ({ value, shiftKey }: CheckInputChanged) => {
      onSelectedChange({ id, value, shiftKey });
    },
    [id, onSelectedChange]
  );

  useEffect(() => {
    handleSelectedChange.current({
      id,
      value: initialIsSelected.current,
      shiftKey: false,
    });

    return () => {
      handleSelectedChange.current({ id, value: null, shiftKey: false });
    };
  }, [id]);

  return (
    <TableRowCell className={classNames('w-[30px]', className)}>
      <CheckInput
        className="m-0!"
        name={id.toString()}
        ariaLabel={translate('SelectRow')}
        value={isSelected}
        {...otherProps}
        onChange={handleChange}
      />
    </TableRowCell>
  );
}

export default TableSelectCell;
