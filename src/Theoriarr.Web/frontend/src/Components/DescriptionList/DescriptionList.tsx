import React from 'react';

export const DESCRIPTION_LIST_CLASS = 'mt-0 mb-0';

interface DescriptionListProps {
  className?: string;
  children?: React.ReactNode;
}

function DescriptionList(props: DescriptionListProps) {
  const { className = DESCRIPTION_LIST_CLASS, children } = props;

  return <dl className={className}>{children}</dl>;
}

export default DescriptionList;
