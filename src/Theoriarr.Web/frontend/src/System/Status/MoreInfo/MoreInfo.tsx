import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItemDescription from 'Components/DescriptionList/DescriptionListItemDescription';
import DescriptionListItemTitle from 'Components/DescriptionList/DescriptionListItemTitle';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

const REPOSITORY_URL = 'https://github.com/MagicBOTAlex/Theoriarr';

function MoreInfo() {
  return (
    <FieldSet legend={translate('MoreInfo')}>
      <DescriptionList>
        <DescriptionListItemTitle>
          {translate('Source')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to={REPOSITORY_URL}>github.com/MagicBOTAlex/Theoriarr</Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>Issues</DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to={`${REPOSITORY_URL}/issues`}>
            github.com/MagicBOTAlex/Theoriarr/issues
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>Credits</DescriptionListItemTitle>
        <DescriptionListItemDescription>
          Theoriarr is built on Sonarr and Radarr (both GPL-3.0). All credit for
          the underlying applications belongs to their respective teams and
          contributors.
        </DescriptionListItemDescription>
      </DescriptionList>
    </FieldSet>
  );
}

export default MoreInfo;
