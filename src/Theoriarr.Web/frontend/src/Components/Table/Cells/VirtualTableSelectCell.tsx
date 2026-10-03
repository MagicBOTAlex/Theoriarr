import classNames from 'classnames';
import React, { useCallback } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import { CheckInputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import VirtualTableRowCell, {
  VirtualTableRowCellProps,
} from './VirtualTableRowCell';

interface VirtualTableSelectCellProps<T extends number | string = number>
  extends VirtualTableRowCellProps {
  inputClassName?: string;
  id: T;
  isSelected?: boolean;
  isDisabled: boolean;
  onSelectedChange: (options: SelectStateInputProps<T>) => void;
}

function VirtualTableSelectCell<T extends number | string = number>({
  inputClassName = 'm-0!',
  className,
  id,
  isSelected = false,
  isDisabled,
  onSelectedChange,
  ...otherProps
}: VirtualTableSelectCellProps<T>) {
  const handleChange = useCallback(
    ({ value, shiftKey }: CheckInputChanged) => {
      onSelectedChange({ id, value, shiftKey });
    },
    [id, onSelectedChange]
  );

  return (
    <VirtualTableRowCell
      className={classNames('flex-[0_0_36px]', className)}
      {...otherProps}
    >
      <CheckInput
        className={inputClassName}
        name={id.toString()}
        ariaLabel={translate('SelectRow')}
        value={isSelected}
        isDisabled={isDisabled}
        onChange={handleChange}
      />
    </VirtualTableRowCell>
  );
}

export default VirtualTableSelectCell;
