import classNames from 'classnames';
import React, {
  ChangeEvent,
  ComponentProps,
  SyntheticEvent,
  useCallback,
} from 'react';
import { InputChanged } from 'typings/inputs';
import {
  INPUT_BORDER_VISIBLE_CLASS,
  INPUT_CLASS,
  INPUT_ERROR_CLASS,
  INPUT_WARNING_CLASS,
} from './InputClassNames';

export const SELECT_INPUT_CLASS = classNames(
  INPUT_CLASS,
  INPUT_BORDER_VISIBLE_CLASS,
  'p-[0_11px]!'
);

const SELECT_INPUT_DISABLED_CLASS = 'opacity-70 cursor-not-allowed';

export interface SelectInputOption
  extends Pick<ComponentProps<'option'>, 'disabled'> {
  key: string | number;
  value: string | number | (() => string | number);
}

interface SelectInputProps<T> {
  className?: string;
  disabledClassName?: string;
  name: string;
  value: string | number;
  values: SelectInputOption[];
  isDisabled?: boolean;
  hasError?: boolean;
  hasWarning?: boolean;
  autoFocus?: boolean;
  onChange: (change: InputChanged<T>) => void;
  onBlur?: (event: SyntheticEvent) => void;
}

function SelectInput<T>({
  className = SELECT_INPUT_CLASS,
  disabledClassName = SELECT_INPUT_DISABLED_CLASS,
  name,
  value,
  values,
  isDisabled = false,
  hasError,
  hasWarning,
  autoFocus = false,
  onBlur,
  onChange,
}: SelectInputProps<T>) {
  const handleChange = useCallback(
    (event: ChangeEvent<HTMLSelectElement>) => {
      onChange({
        name,
        value: event.target.value as T,
      });
    },
    [name, onChange]
  );

  return (
    <select
      className={classNames(
        className,
        hasError && INPUT_ERROR_CLASS,
        hasWarning && INPUT_WARNING_CLASS,
        isDisabled && disabledClassName
      )}
      disabled={isDisabled}
      name={name}
      value={value}
      autoFocus={autoFocus}
      onChange={handleChange}
      onBlur={onBlur}
    >
      {values.map((option) => {
        const { key, value: optionValue, ...otherOptionProps } = option;

        return (
          <option key={key} value={key} {...otherOptionProps}>
            {typeof optionValue === 'function' ? optionValue() : optionValue}
          </option>
        );
      })}
    </select>
  );
}

export default SelectInput;
