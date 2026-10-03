import classNames from 'classnames';
import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import Link, { LinkProps } from 'Components/Link/Link';
import { icons } from 'Helpers/Props';

export const TOOLBAR_BUTTON_CLASS =
  'w-15 pt-1 text-center hover:text-[var(--toobarButtonHoverColor)]';

export const TOOLBAR_BUTTON_DISABLED_CLASS = 'text-[var(--disabledColor)]';

export const TOOLBAR_BUTTON_LABEL_CONTAINER_CLASS =
  'flex h-6 items-center justify-center overflow-hidden';

export const TOOLBAR_BUTTON_LABEL_CLASS =
  'max-h-full max-w-full px-[3px] text-[11px] leading-[12px] text-[var(--toolbarLabelColor)]';

export interface PageToolbarButtonProps extends LinkProps {
  label: string;
  iconName: IconName;
  spinningName?: IconName;
  isSpinning?: boolean;
  isDisabled?: boolean;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  overflowComponent?: React.ComponentType<any>;
}

function PageToolbarButton({
  label,
  iconName,
  spinningName = icons.SPINNER,
  isDisabled = false,
  isSpinning = false,
  overflowComponent,
  ...otherProps
}: PageToolbarButtonProps) {
  return (
    <Link
      className={classNames(
        TOOLBAR_BUTTON_CLASS,
        isDisabled && TOOLBAR_BUTTON_DISABLED_CLASS
      )}
      isDisabled={isDisabled || isSpinning}
      title={label}
      {...otherProps}
    >
      <Icon
        name={isSpinning ? spinningName || iconName : iconName}
        isSpinning={isSpinning}
        size={21}
      />

      <div className={TOOLBAR_BUTTON_LABEL_CONTAINER_CLASS}>
        <div className={TOOLBAR_BUTTON_LABEL_CLASS}>{label}</div>
      </div>
    </Link>
  );
}

export default PageToolbarButton;
