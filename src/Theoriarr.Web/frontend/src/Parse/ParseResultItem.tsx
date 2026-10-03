import React, { ReactNode } from 'react';

interface ParseResultItemProps {
  title: string;
  data: string | number | ReactNode;
}

function ParseResultItem(props: ParseResultItemProps) {
  const { title, data } = props;

  return (
    <div className="mb-[10px] block md:mb-0 md:flex">
      <div className="mr-5 w-[250px] text-left font-bold md:text-right">
        {title}
      </div>
      <div>{data}</div>
    </div>
  );
}

export default ParseResultItem;
