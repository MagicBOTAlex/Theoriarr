import classNames from 'classnames';
import React, { ComponentProps, ReactNode } from 'react';

interface StatusIndicatorProps extends ComponentProps<'span'> {
  label: string;
  children: ReactNode;
}

export default function StatusIndicator({
  className,
  label,
  children,
  ...otherProps
}: StatusIndicatorProps) {
  return (
    <span className={classNames('inline-flex', className)} {...otherProps}>
      <span className="sr-only">{label}</span>
      {children}
    </span>
  );
}
