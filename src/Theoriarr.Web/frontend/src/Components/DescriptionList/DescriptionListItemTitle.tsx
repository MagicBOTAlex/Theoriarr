import React, { ReactNode } from 'react';

export const DESCRIPTION_ITEM_TITLE_CLASS =
  'leading-[1.528571429] font-bold md:float-left md:clear-left md:overflow-hidden! md:max-w-full md:w-[160px] md:text-right md:text-ellipsis! md:whitespace-nowrap!';

export interface DescriptionListItemTitleProps {
  className?: string;
  children?: ReactNode;
}

function DescriptionListItemTitle(props: DescriptionListItemTitleProps) {
  const { className = DESCRIPTION_ITEM_TITLE_CLASS, children } = props;

  return <dt className={className}>{children}</dt>;
}

export default DescriptionListItemTitle;
