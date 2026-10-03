import classNames from 'classnames';
import React, { ReactNode } from 'react';
import { Size } from 'Helpers/Props/sizes';

const LABEL_CLASS =
  'flex justify-end min-h-[35px] pt-2 pr-5 text-end font-bold max-[1200px]:justify-start';

const ERROR_CLASS = 'text-[var(--dangerColor)]';

const ADVANCED_CLASS = 'text-[var(--advancedFormLabelColor)]';

const SIZE_CLASSES = {
  small: 'flex-[0_0_150px]',
  large: 'flex-[0_0_250px]',
};

interface FormLabelProps {
  children: ReactNode;
  className?: string;
  errorClassName?: string;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  name?: string;
  hasError?: boolean;
  isAdvanced?: boolean;
}

function FormLabel(props: FormLabelProps) {
  const {
    children,
    className = LABEL_CLASS,
    errorClassName = ERROR_CLASS,
    size = 'large',
    name,
    hasError,
    isAdvanced = false,
  } = props;

  return (
    <label
      className={classNames(
        className,
        SIZE_CLASSES[size],
        hasError && errorClassName,
        isAdvanced && ADVANCED_CLASS
      )}
      htmlFor={name}
    >
      {children}
    </label>
  );
}

export default FormLabel;
