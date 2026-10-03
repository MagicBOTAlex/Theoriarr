import React, { useMemo } from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import getFinaleTypeName from './getFinaleTypeName';

interface SeriesStatusCellProps {
  finaleType: string;
}

function FinaleType(props: SeriesStatusCellProps) {
  const { finaleType } = props;

  const finaleText = useMemo(() => {
    return getFinaleTypeName(finaleType);
  }, [finaleType]);

  if (finaleType == null || finaleText == null) {
    return null;
  }

  return (
    <Label
      className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px]"
      kind={kinds.INFO}
    >
      {finaleText}
    </Label>
  );
}

export default FinaleType;
