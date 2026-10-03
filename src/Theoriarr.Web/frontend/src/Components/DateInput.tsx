import React, { useCallback, useRef } from 'react';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import { icons } from 'Helpers/Props';
import { isCrossOriginFrame } from 'Utilities/browser';

export const DATE_INPUT_CLASS = 'relative inline-block align-middle';
export const DATE_INPUT_INPUT_CLASS =
  'absolute top-0 left-0 w-full h-full border-0 opacity-0 pointer-events-none pointer-coarse:pointer-events-auto';

const hasDatePicker =
  window.matchMedia('(pointer: coarse)').matches ||
  ('showPicker' in HTMLInputElement.prototype && !isCrossOriginFrame());

interface DateInputProps {
  className?: string;
  value: string;
  label: string;
  isDisabled?: boolean;
  onChange: (value: string) => void;
}

function DateInput({
  className = DATE_INPUT_CLASS,
  value,
  label,
  isDisabled = false,
  onChange,
}: DateInputProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  const handlePress = useCallback(() => {
    inputRef.current?.showPicker();
  }, []);

  const handleChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const { value: newValue } = event.target;

      if (newValue) {
        onChange(newValue);
      }
    },
    [onChange]
  );

  if (!hasDatePicker) {
    return null;
  }

  return (
    <span className={className}>
      <Button
        isDisabled={isDisabled}
        aria-label={label}
        title={label}
        onPress={handlePress}
      >
        <Icon name={icons.CALENDAR_O} />
      </Button>

      <input
        ref={inputRef}
        type="date"
        className={DATE_INPUT_INPUT_CLASS}
        value={value}
        aria-label={label}
        tabIndex={-1}
        onChange={handleChange}
      />
    </span>
  );
}

export default DateInput;
