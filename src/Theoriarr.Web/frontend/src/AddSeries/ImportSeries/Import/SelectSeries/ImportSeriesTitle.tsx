import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

interface ImportSeriesTitleProps {
  title: string;
  year: number;
  network?: string;
  isExistingSeries: boolean;
}

function ImportSeriesTitle({
  title,
  year,
  network,
  isExistingSeries,
}: ImportSeriesTitleProps) {
  return (
    <div className="flex items-center shrink basis-auto overflow-hidden">
      <div className="max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
        {title}
      </div>

      {year > 0 && !title.includes(String(year)) ? (
        <span className="mx-[5px] text-[var(--disabledColor)]">({year})</span>
      ) : null}

      {network ? <Label>{network}</Label> : null}

      {isExistingSeries ? (
        <Label kind={kinds.WARNING}>{translate('Existing')}</Label>
      ) : null}
    </div>
  );
}

export default ImportSeriesTitle;
