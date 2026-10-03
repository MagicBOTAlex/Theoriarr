import React, { ForwardedRef, forwardRef, ReactNode } from 'react';
import { useModalContext } from './ModalContext';

interface ModalHeaderProps extends React.HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
}

const ModalHeader = forwardRef(
  (
    { children, ...otherProps }: ModalHeaderProps,
    ref: ForwardedRef<HTMLDivElement>
  ) => {
    const { headerId } = useModalContext();

    return (
      <div
        ref={ref}
        id={headerId}
        className="max-w-full shrink-0 truncate border-b border-[var(--borderColor)] bg-[color-mix(in_srgb,var(--textColor),transparent_97%)] py-4 pr-14 pl-5 text-[15px] font-semibold"
        {...otherProps}
      >
        {children}
      </div>
    );
  }
);

export default ModalHeader;
