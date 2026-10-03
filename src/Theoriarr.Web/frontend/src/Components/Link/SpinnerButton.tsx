import classNames from 'classnames';
import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import { icons } from 'Helpers/Props';
import Button, { ButtonProps } from './Button';

export const SPINNER_BUTTON_CLASS = 'relative';

export const SPINNER_BUTTON_CONTAINER_CLASS =
  'absolute top-1/2 left-[-100%] inline-flex invisible [transition:left_0.2s] -translate-x-1/2 -translate-y-1/2';

export const SPINNER_BUTTON_CONTAINER_SPINNING_CLASS =
  'absolute top-1/2 left-1/2 inline-flex visible [transition:left_0.2s] -translate-x-1/2 -translate-y-1/2';

export const SPINNER_BUTTON_LABEL_CLASS =
  'relative left-0 [transition:left_0.2s,opacity_0.2s]';

export const SPINNER_BUTTON_LABEL_SPINNING_CLASS =
  'relative left-full invisible [transition:left_0.2s,opacity_0.2s]';

export interface SpinnerButtonProps extends ButtonProps {
  isSpinning: boolean;
  isDisabled?: boolean;
  spinnerIcon?: IconName;
}

function SpinnerButton({
  className = '',
  isSpinning,
  isDisabled,
  spinnerIcon = icons.SPINNER,
  children,
  ...otherProps
}: SpinnerButtonProps) {
  return (
    <Button
      className={classNames(SPINNER_BUTTON_CLASS, className)}
      isDisabled={isDisabled || isSpinning}
      {...otherProps}
    >
      <span
        className={
          isSpinning
            ? SPINNER_BUTTON_CONTAINER_SPINNING_CLASS
            : SPINNER_BUTTON_CONTAINER_CLASS
        }
      >
        <Icon className="z-[1]" name={spinnerIcon} isSpinning={true} />
      </span>

      <span
        className={
          isSpinning
            ? SPINNER_BUTTON_LABEL_SPINNING_CLASS
            : SPINNER_BUTTON_LABEL_CLASS
        }
      >
        {children}
      </span>
    </Button>
  );
}

export default SpinnerButton;
