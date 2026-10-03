import classNames from 'classnames';
import React, { ComponentProps, ReactNode } from 'react';
import { kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import { Size } from 'Helpers/Props/sizes';

const LABEL_CLASS =
  'inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default';

const KIND_CLASSES = {
  danger:
    'border-[var(--dangerColor)] bg-[var(--dangerColor)] text-[var(--white)]',
  default:
    'border-[var(--themeLightColor)] bg-[var(--themeLightColor)] text-[var(--white)]',
  disabled:
    'border-[var(--disabledColor)] bg-[var(--disabledColor)] text-[var(--white)]',
  info: 'border-[var(--infoColor)] bg-[var(--infoColor)] text-[var(--infoTextColor)]',
  inverse:
    'border-[var(--inverseLabelColor)] bg-[var(--inverseLabelColor)] text-[var(--inverseLabelTextColor)]',
  primary:
    'border-[var(--primaryColor)] bg-[var(--primaryColor)] text-[var(--white)]',
  purple: 'border-[var(--purple)] bg-[var(--purple)] text-[var(--white)]',
  queue:
    'border-[var(--queueColor)] bg-[var(--queueColor)] text-[var(--white)]',
  success: 'border-[var(--successColor)] bg-[var(--successColor)] text-[#eee]',
  warning:
    'border-[var(--warningColor)] bg-[var(--warningColor)] text-[var(--white)]',
} as const;

const OUTLINE_CLASSES = {
  danger: 'bg-[var(--disabledLabelColor)]! text-[var(--dangerColor)]!',
  default: 'bg-[var(--disabledLabelColor)]! text-[var(--themeLightColor)]!',
  disabled: 'bg-[var(--disabledLabelColor)]! text-[var(--white)]!',
  info: 'bg-[var(--disabledLabelColor)]! text-[var(--infoColor)]!',
  inverse:
    'bg-[var(--inverseLabelTextColor)]! text-[var(--inverseLabelColor)]!',
  primary: 'bg-[var(--disabledLabelColor)]! text-[var(--primaryColor)]!',
  purple: 'bg-[var(--disabledLabelColor)]! text-[var(--purple)]!',
  queue: 'bg-[var(--disabledLabelColor)]! text-[var(--queueColor)]!',
  success: 'bg-[var(--disabledLabelColor)]! text-[var(--successColor)]!',
  warning: 'bg-[var(--disabledLabelColor)]! text-[var(--warningColor)]!',
} as const;

const SIZE_CLASSES = {
  large: 'rounded-[6px] px-[7px] py-[3px] text-[14px] font-bold',
  medium: 'rounded-[4px] px-[5px] py-[2px] text-[12px]',
  small: 'px-[3px] py-[1px] text-[11px]',
} as const;

export interface LabelProps extends ComponentProps<'span'> {
  kind?: Extract<Kind, keyof typeof KIND_CLASSES>;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  outline?: boolean;
  children: ReactNode;
}

export default function Label({
  className = LABEL_CLASS,
  kind = kinds.DEFAULT,
  size = sizes.SMALL,
  outline = false,
  ...otherProps
}: LabelProps) {
  return (
    <span
      className={classNames(
        className,
        KIND_CLASSES[kind],
        outline && OUTLINE_CLASSES[kind],
        SIZE_CLASSES[size]
      )}
      {...otherProps}
    />
  );
}
