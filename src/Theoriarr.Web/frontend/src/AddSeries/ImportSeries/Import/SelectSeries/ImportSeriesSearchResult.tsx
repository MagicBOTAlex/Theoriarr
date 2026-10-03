import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import useExistingSeries from 'Series/useExistingSeries';
import ImportSeriesTitle from './ImportSeriesTitle';

interface ImportSeriesSearchResultProps {
  tvdbId: number;
  title: string;
  year: number;
  network?: string;
  onPress: (tvdbId: number) => void;
}

function ImportSeriesSearchResult({
  tvdbId,
  title,
  year,
  network,
  onPress,
}: ImportSeriesSearchResultProps) {
  const isExistingSeries = useExistingSeries(tvdbId);

  const handlePress = useCallback(() => {
    onPress(tvdbId);
  }, [tvdbId, onPress]);

  return (
    <div className="flex px-[20px] py-[10px] w-full hover:bg-[var(--menuItemHoverBackgroundColor)]">
      <Link className="flex-[1_0_0] overflow-hidden" onPress={handlePress}>
        <ImportSeriesTitle
          title={title}
          year={year}
          network={network}
          isExistingSeries={isExistingSeries}
        />
      </Link>

      <Link
        className="ml-auto text-[var(--textColor)]"
        to={`https://www.thetvdb.com/?tab=series&id=${tvdbId}`}
      >
        <Icon className="ml-[10px]" name={icons.EXTERNAL_LINK} size={16} />
      </Link>
    </div>
  );
}

export default ImportSeriesSearchResult;
