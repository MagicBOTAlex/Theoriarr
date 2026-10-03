import classNames from 'classnames';
import React from 'react';
import Icon, { IconProps } from 'Components/Icon';
import Link, { LinkProps } from './Link';

const ICON_BUTTON_CLASS =
  'inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)]';
const ICON_BUTTON_DISABLED_CLASS = 'text-[var(--iconButtonDisabledColor)]';

export interface IconButtonProps
  extends Omit<LinkProps, 'name' | 'kind'>,
    Pick<IconProps, 'name' | 'kind' | 'size' | 'isSpinning'> {
  iconClassName?: IconProps['className'];
}

export default function IconButton({
  className = '',
  iconClassName,
  name,
  kind,
  size = 12,
  isSpinning,
  ...otherProps
}: IconButtonProps) {
  return (
    <Link
      className={classNames(
        ICON_BUTTON_CLASS,
        className,
        otherProps.isDisabled && ICON_BUTTON_DISABLED_CLASS
      )}
      {...otherProps}
    >
      <Icon
        className={iconClassName}
        name={name}
        kind={kind}
        size={size}
        isSpinning={isSpinning}
        aria-hidden={true}
      />
    </Link>
  );
}
