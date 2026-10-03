import classNames from 'classnames';
import React from 'react';
import { kinds, sizes } from 'Helpers/Props';
import { Align } from 'Helpers/Props/align';
import { Kind } from 'Helpers/Props/kinds';
import { Size } from 'Helpers/Props/sizes';
import Link, { LinkProps } from './Link';

const BUTTON_CLASS =
  'overflow-hidden border! border-solid rounded-[4px] align-middle text-center whitespace-nowrap leading-[normal] hover:no-underline!';

const KIND_CLASSES = {
  danger:
    'border-[var(--dangerBorderColor)] bg-[var(--dangerBackgroundColor)]! text-[var(--white)]! focus:border-[var(--dangerHoverBorderColor)] hover:border-[var(--dangerHoverBorderColor)] hover:bg-[var(--dangerHoverBackgroundColor)] hover:text-[var(--white)]!',
  default:
    'border-[var(--defaultBorderColor)] bg-[var(--defaultBackgroundColor)]! text-[var(--defaultColor)]! focus:border-[var(--defaultHoverBorderColor)] hover:border-[var(--defaultHoverBorderColor)] hover:bg-[var(--defaultHoverBackgroundColor)] hover:text-[var(--defaultColor)]!',
  primary:
    'border-[var(--primaryBorderColor)] bg-[var(--primaryBackgroundColor)]! text-[var(--white)]! focus:border-[var(--primaryHoverBorderColor)] hover:border-[var(--primaryHoverBorderColor)] hover:bg-[var(--primaryHoverBackgroundColor)] hover:text-[var(--white)]!',
  success:
    'border-[var(--successBorderColor)] bg-[var(--successBackgroundColor)]! text-[var(--white)]! focus:border-[var(--successHoverBorderColor)] hover:border-[var(--successHoverBorderColor)] hover:bg-[var(--successHoverBackgroundColor)] hover:text-[var(--white)]!',
  warning:
    'border-[var(--warningBorderColor)] bg-[var(--warningBackgroundColor)]! text-[var(--white)]! focus:border-[var(--warningHoverBorderColor)] hover:border-[var(--warningHoverBorderColor)] hover:bg-[var(--warningHoverBackgroundColor)] hover:text-[var(--white)]!',
} as const;

const SIZE_CLASSES = {
  small: 'px-[5px] py-[1px] text-[12px]',
  medium: 'px-4 py-1.5 text-[14px]',
  large: 'px-5 py-2.5 text-[16px]',
} as const;

const POSITION_CLASSES = {
  left: '-ml-px! rounded-tr-none rounded-br-none',
  center: '-ml-px! rounded-none',
  right: '-ml-px! rounded-tl-none rounded-bl-none',
} as const;

export interface ButtonProps extends Omit<LinkProps, 'children' | 'size'> {
  buttonGroupPosition?: Extract<Align, keyof typeof POSITION_CLASSES>;
  kind?: Extract<Kind, keyof typeof KIND_CLASSES>;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  children: Required<LinkProps['children']>;
}

export default function Button({
  className = '',
  buttonGroupPosition,
  kind = kinds.DEFAULT,
  size = sizes.MEDIUM,
  ...otherProps
}: ButtonProps) {
  return (
    <Link
      className={classNames(
        BUTTON_CLASS,
        className,
        KIND_CLASSES[kind],
        SIZE_CLASSES[size],
        buttonGroupPosition && POSITION_CLASSES[buttonGroupPosition],
        otherProps.isDisabled && 'opacity-65'
      )}
      {...otherProps}
    />
  );
}
