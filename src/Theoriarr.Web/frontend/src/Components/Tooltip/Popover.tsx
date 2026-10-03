import React from 'react';
import Tooltip, { TooltipProps } from './Tooltip';

interface PopoverProps extends Omit<TooltipProps, 'tooltip' | 'bodyClassName'> {
  title: string;
  body: React.ReactNode;
}

function Popover({ title, body, ...otherProps }: PopoverProps) {
  return (
    <Tooltip
      {...otherProps}
      bodyClassName="p-0"
      tooltip={
        <div>
          <div className="border-b border-[var(--popoverTitleBorderColor)] bg-[var(--popoverTitleBackgroundColor)] px-5 py-2.5 text-base">
            {title}
          </div>

          <div className="overflow-auto bg-[var(--popoverBodyBackgroundColor)] p-2.5">
            {body}
          </div>
        </div>
      }
    />
  );
}

export default Popover;
