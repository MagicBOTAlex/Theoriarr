import classNames from 'classnames';
import React, { SyntheticEvent, useCallback, useEffect, useRef } from 'react';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import { CheckInputChanged } from 'typings/inputs';
import FormInputHelpText from './FormInputHelpText';

const CONTAINER_CLASS = 'relative flex flex-[1_1_65%] select-none';

const LABEL_CLASS = 'flex mb-0 min-h-[21px] font-normal cursor-pointer';

const CHECKBOX_CLASS =
  'peer absolute opacity-0 cursor-pointer pointer-events-none';

const INPUT_CLASS =
  'flex-[1_0_auto] mt-[7px] mr-[5px] w-5 h-5 border border-solid border-[#ccc] rounded-[2px] bg-[var(--white)] text-[var(--white)] text-center leading-[20px] peer-focus:outline-0 peer-focus:border-[var(--inputFocusBorderColor)] peer-focus:shadow-[inset_0_1px_1px_var(--inputBoxShadowColor),0_0_8px_var(--inputFocusBoxShadowColor)]';

const KIND_CLASSES = {
  danger: 'border-[var(--dangerColor)] bg-[var(--dangerColor)]!',
  primary: 'border-[var(--primaryColor)] bg-[var(--primaryColor)]!',
  success: 'border-[var(--successColor)] bg-[var(--successColor)]!',
  warning: 'border-[var(--warningColor)] bg-[var(--warningColor)]!',
} as const;

const NOT_CHECKED_DISABLED_CLASS =
  'border-[var(--disabledCheckInputColor)] bg-[var(--disabledCheckInputColor)]!';

const INDETERMINATE_CLASS = 'border-[var(--gray)] bg-[var(--gray)]!';

const DISABLED_CLASS = 'opacity-70 cursor-not-allowed';

interface ChangeEvent<T = Element> extends SyntheticEvent<T, MouseEvent> {
  target: EventTarget & T;
}

export interface CheckInputProps {
  ariaLabel?: string;
  className?: string;
  containerClassName?: string;
  name: string;
  checkedValue?: boolean;
  uncheckedValue?: boolean;
  value?: string | boolean | null;
  helpText?: string;
  helpTextWarning?: string;
  isDisabled?: boolean;
  kind?: Extract<Kind, keyof typeof KIND_CLASSES>;
  onChange: (changes: CheckInputChanged) => void;
}

function CheckInput(props: CheckInputProps) {
  const {
    ariaLabel,
    className,
    containerClassName,
    name,
    value,
    checkedValue = true,
    uncheckedValue = false,
    helpText,
    helpTextWarning,
    isDisabled,
    kind = 'primary',
    onChange,
  } = props;

  const inputRef = useRef<HTMLInputElement>(null);

  const isChecked = value === checkedValue;
  const isUnchecked = value === uncheckedValue;
  const isIndeterminate = !isChecked && !isUnchecked;

  const toggleChecked = useCallback(
    (checked: boolean, shiftKey: boolean) => {
      const newValue = checked ? checkedValue : uncheckedValue;

      if (value !== newValue) {
        onChange({
          name,
          value: newValue,
          shiftKey,
        });
      }
    },
    [name, value, checkedValue, uncheckedValue, onChange]
  );

  const handleClick = useCallback(
    (event: SyntheticEvent<HTMLElement, MouseEvent>) => {
      if (isDisabled) {
        return;
      }

      if (event.target === inputRef.current) {
        return;
      }

      const shiftKey = event.nativeEvent.shiftKey;
      const checked = !(inputRef.current?.checked ?? false);

      event.preventDefault();
      toggleChecked(checked, shiftKey);
    },
    [isDisabled, toggleChecked]
  );

  const handleChange = useCallback(
    (event: ChangeEvent<HTMLInputElement>) => {
      const checked = event.target.checked;
      const shiftKey = event.nativeEvent.shiftKey;

      toggleChecked(checked, shiftKey);
    },
    [toggleChecked]
  );

  useEffect(() => {
    if (!inputRef.current) {
      return;
    }

    inputRef.current.indeterminate =
      value !== uncheckedValue && value !== checkedValue;
  }, [value, uncheckedValue, checkedValue]);

  return (
    <div className={containerClassName ?? CONTAINER_CLASS}>
      <label className={LABEL_CLASS} onClick={handleClick}>
        <input
          ref={inputRef}
          className={CHECKBOX_CLASS}
          type="checkbox"
          name={name}
          aria-label={ariaLabel}
          checked={isChecked}
          disabled={isDisabled}
          onChange={handleChange}
        />

        <div
          className={classNames(
            INPUT_CLASS,
            className,
            isChecked ? KIND_CLASSES[kind] : undefined,
            !isChecked && isDisabled && NOT_CHECKED_DISABLED_CLASS,
            isIndeterminate && INDETERMINATE_CLASS,
            isDisabled && DISABLED_CLASS
          )}
        >
          {isChecked ? <Icon name={icons.CHECK} /> : null}

          {isIndeterminate ? <Icon name={icons.CHECK_INDETERMINATE} /> : null}
        </div>

        {helpText ? (
          <FormInputHelpText className="mt-2! ml-[5px]" text={helpText} />
        ) : null}

        {!helpText && helpTextWarning ? (
          <FormInputHelpText
            className="mt-2! ml-[5px]"
            text={helpTextWarning}
            isWarning={true}
          />
        ) : null}
      </label>
    </div>
  );
}

export default CheckInput;
