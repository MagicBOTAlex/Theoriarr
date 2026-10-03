import React, { ReactNode } from 'react';

export const DESCRIPTION_ITEM_DESCRIPTION_CLASS =
  'ml-0 leading-[1.528571429] break-words md:ml-[180px]';

export interface DescriptionListItemDescriptionProps {
  className?: string;
  children?: ReactNode;
}

function DescriptionListItemDescription(
  props: DescriptionListItemDescriptionProps
) {
  const { className = DESCRIPTION_ITEM_DESCRIPTION_CLASS, children } = props;

  return <dd className={className}>{children}</dd>;
}

export default DescriptionListItemDescription;
