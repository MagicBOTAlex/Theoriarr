import classNames from 'classnames';
import React, { ForwardedRef, forwardRef, ReactNode } from 'react';

export const MODAL_FOOTER_CLASS =
  'flex items-center justify-end shrink-0 px-5 py-4 border-t border-[var(--borderColor)] bg-[color-mix(in_srgb,var(--textColor),transparent_97%)] max-[768px]:px-4 max-[768px]:py-3 [&_a]:ml-[10px] [&_button]:ml-[10px] [&_a:first-child]:ml-0 [&_button:first-child]:ml-0';

interface ModalFooterProps extends React.HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
}

const ModalFooter = forwardRef(
  (
    { className, children, ...otherProps }: ModalFooterProps,
    ref: ForwardedRef<HTMLDivElement>
  ) => {
    return (
      <div
        ref={ref}
        className={classNames(MODAL_FOOTER_CLASS, className)}
        {...otherProps}
      >
        {children}
      </div>
    );
  }
);

export default ModalFooter;
