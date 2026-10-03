import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

interface SelectSeasonRowProps {
  seasonNumber: number;
  onSeasonSelect(season: number): unknown;
}

function SelectSeasonRow(props: SelectSeasonRowProps) {
  const { seasonNumber, onSeasonSelect } = props;

  const onSeasonSelectWrapper = useCallback(() => {
    onSeasonSelect(seasonNumber);
  }, [seasonNumber, onSeasonSelect]);

  return (
    <Link
      className="border-b border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] p-2"
      component="div"
      onPress={onSeasonSelectWrapper}
    >
      {seasonNumber === 0
        ? translate('Specials')
        : translate('SeasonNumberToken', { seasonNumber })}
    </Link>
  );
}

export default SelectSeasonRow;
