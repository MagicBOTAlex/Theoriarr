import classNames from 'classnames';
import React, { ComponentProps, ReactNode } from 'react';
import { Size } from 'Helpers/Props/sizes';

const LABEL_CLASS =
  'inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit';

const SIZE_CLASSES = {
  large: 'px-[7px] py-[3px] text-[14px]',
  medium: 'px-[5px] py-[2px] text-[12px]',
  small: 'px-[3px] py-[1px] text-[11px]',
} as const;

interface InfoLabelProps extends ComponentProps<'span'> {
  className?: string;
  name: string;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  outline?: boolean;
  children: ReactNode;
}

function InfoLabel({
  className = LABEL_CLASS,
  name,
  size = 'small',
  outline = false,
  children,
  ...otherProps
}: InfoLabelProps) {
  return (
    <span
      className={classNames(
        className,
        SIZE_CLASSES[size],
        outline && 'bg-[var(--white)]'
      )}
      {...otherProps}
    >
      <div className="mb-[2px] text-[10px] text-[var(--helpTextColor)]">
        {name}
      </div>
      <div>{children}</div>
    </span>
  );
}

export default InfoLabel;
