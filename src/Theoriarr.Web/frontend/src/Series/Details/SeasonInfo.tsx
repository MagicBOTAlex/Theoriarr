import classNames from 'classnames';
import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import { DESCRIPTION_ITEM_DESCRIPTION_CLASS } from 'Components/DescriptionList/DescriptionListItemDescription';
import { DESCRIPTION_ITEM_TITLE_CLASS } from 'Components/DescriptionList/DescriptionListItemTitle';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';

const TITLE_CLASS = classNames(DESCRIPTION_ITEM_TITLE_CLASS, 'w-[90px]!');
const DESCRIPTION_CLASS = classNames(
  DESCRIPTION_ITEM_DESCRIPTION_CLASS,
  'ml-[110px]!'
);

interface SeasonInfoProps {
  totalEpisodeCount: number;
  monitoredEpisodeCount: number;
  episodeFileCount: number;
  sizeOnDisk: number;
}

function SeasonInfo({
  totalEpisodeCount,
  monitoredEpisodeCount,
  episodeFileCount,
  sizeOnDisk,
}: SeasonInfoProps) {
  return (
    <DescriptionList>
      <DescriptionListItem
        titleClassName={TITLE_CLASS}
        descriptionClassName={DESCRIPTION_CLASS}
        title={translate('Total')}
        data={totalEpisodeCount}
      />

      <DescriptionListItem
        titleClassName={TITLE_CLASS}
        descriptionClassName={DESCRIPTION_CLASS}
        title={translate('Monitored')}
        data={monitoredEpisodeCount}
      />

      <DescriptionListItem
        titleClassName={TITLE_CLASS}
        descriptionClassName={DESCRIPTION_CLASS}
        title={translate('WithFiles')}
        data={episodeFileCount}
      />

      <DescriptionListItem
        titleClassName={TITLE_CLASS}
        descriptionClassName={DESCRIPTION_CLASS}
        title={translate('SizeOnDisk')}
        data={formatBytes(sizeOnDisk)}
      />
    </DescriptionList>
  );
}

export default SeasonInfo;
