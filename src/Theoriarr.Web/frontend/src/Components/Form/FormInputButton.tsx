import classNames from 'classnames';
import React from 'react';
import { IconName } from 'Components/Icon';
import Button, { ButtonProps } from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import { kinds } from 'Helpers/Props';

const BUTTON_CLASS = 'border-l-0! rounded-tl-none rounded-bl-none';
const MIDDLE_BUTTON_CLASS = 'rounded-tr-none rounded-br-none';

export interface FormInputButtonProps extends ButtonProps {
  canSpin?: boolean;
  isLastButton?: boolean;
  isSpinning?: boolean;
  spinnerIcon?: IconName;
}

function FormInputButton({
  className = '',
  canSpin = false,
  isLastButton = true,
  isSpinning = false,
  kind = kinds.PRIMARY,
  ...otherProps
}: FormInputButtonProps) {
  const buttonClass = classNames(
    BUTTON_CLASS,
    className,
    !isLastButton && MIDDLE_BUTTON_CLASS
  );

  if (canSpin) {
    return (
      <SpinnerButton
        className={buttonClass}
        kind={kind}
        isSpinning={isSpinning}
        {...otherProps}
      />
    );
  }

  return <Button className={buttonClass} kind={kind} {...otherProps} />;
}

export default FormInputButton;
