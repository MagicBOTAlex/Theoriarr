import React from 'react';
import Icon, { IconName } from 'Components/Icon';

interface LibraryIndexOverviewInfoRowProps {
  title?: string;
  iconName: IconName;
  label: string | null;
}

function LibraryIndexOverviewInfoRow(props: LibraryIndexOverviewInfoRowProps) {
  const { title, iconName, label } = props;

  return (
    <div className="my-[2px] shrink-0 basis-[21px]" title={title}>
      <Icon
        className="mr-[5px] w-[25px]! text-center"
        name={iconName}
        size={14}
      />

      {label}
    </div>
  );
}

export default LibraryIndexOverviewInfoRow;
