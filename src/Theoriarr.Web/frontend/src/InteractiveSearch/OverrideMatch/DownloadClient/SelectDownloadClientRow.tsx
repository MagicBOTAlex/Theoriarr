import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

interface SelectSeasonRowProps {
  id: number;
  name: string;
  priority: number;
  onDownloadClientSelect(downloadClientId: number): unknown;
}

function SelectDownloadClientRow(props: SelectSeasonRowProps) {
  const { id, name, priority, onDownloadClientSelect } = props;

  const onSeasonSelectWrapper = useCallback(() => {
    onDownloadClientSelect(id);
  }, [id, onDownloadClientSelect]);

  return (
    <Link
      className="flex justify-between border-b border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] p-2"
      component="div"
      onPress={onSeasonSelectWrapper}
    >
      <div>{name}</div>
      <div>{translate('PrioritySettings', { priority })}</div>
    </Link>
  );
}

export default SelectDownloadClientRow;
