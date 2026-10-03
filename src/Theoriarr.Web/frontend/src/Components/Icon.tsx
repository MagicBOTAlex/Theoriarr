import {
  FontAwesomeIcon,
  FontAwesomeIconProps,
} from '@fortawesome/react-fontawesome';
import classNames from 'classnames';
import React, { ComponentProps } from 'react';
import { kinds } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';

// `color` is inherited, so `default` intentionally applies no class and lets the
// caller's own class win.
const KIND_CLASSES = {
  danger: 'text-[var(--dangerColor)]',
  default: '',
  disabled: 'text-[var(--disabledColor)]',
  info: 'text-[var(--infoColor)]',
  pink: 'text-[var(--pink)]',
  primary: 'text-[var(--primaryColor)]',
  purple: 'text-[var(--purple)]',
  success: 'text-[var(--successColor)]',
  warning: 'text-[var(--warningColor)]',
};

export type IconName = FontAwesomeIconProps['icon'];
export type IconKind = Extract<Kind, keyof typeof KIND_CLASSES>;

export interface IconProps
  extends Omit<
    FontAwesomeIconProps,
    'icon' | 'spin' | 'name' | 'title' | 'size'
  > {
  containerClassName?: ComponentProps<'span'>['className'];
  name: IconName;
  kind?: IconKind;
  size?: number;
  isSpinning?: FontAwesomeIconProps['spin'];
  title?: string | (() => string) | null;
}

function GpuIcon({
  className,
  kind,
  size,
}: {
  className?: string;
  kind: IconKind;
  size: number;
}) {
  return (
    <svg
      aria-hidden="true"
      className={classNames(className, KIND_CLASSES[kind])}
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M2 17h18a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2H2" />
      <path d="M2 21V3" />
      <path d="M7 17v3a1 1 0 0 0 1 1h5a1 1 0 0 0 1-1v-3" />
      <circle cx="16" cy="11" r="2" />
      <circle cx="8" cy="11" r="2" />
    </svg>
  );
}

export default function Icon({
  containerClassName,
  className,
  name,
  kind = kinds.DEFAULT,
  size = 14,
  title,
  isSpinning = false,
  fixedWidth = false,
  ...otherProps
}: IconProps) {
  if (name === 'gpu') {
    const gpuIcon = <GpuIcon className={className} kind={kind} size={size} />;

    if (title) {
      return (
        <span
          className={containerClassName}
          title={typeof title === 'function' ? title() : title}
        >
          {gpuIcon}
        </span>
      );
    }

    return gpuIcon;
  }

  const icon = (
    <FontAwesomeIcon
      className={classNames(className, KIND_CLASSES[kind])}
      icon={name}
      spin={isSpinning}
      fixedWidth={fixedWidth}
      style={{
        fontSize: `${size}px`,
      }}
      {...otherProps}
    />
  );

  if (title) {
    return (
      <span
        className={containerClassName}
        title={typeof title === 'function' ? title() : title}
      >
        {icon}
      </span>
    );
  }

  return icon;
}
