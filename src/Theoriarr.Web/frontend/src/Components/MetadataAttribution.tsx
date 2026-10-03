import React from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

export default function MetadataAttribution() {
  return (
    <div className="flex justify-end mt-auto">
      <Link
        className="flex justify-end mt-auto text-sm text-[var(--mediumGray)] hover:text-[var(--darkGray)] hover:no-underline"
        to="/settings/metadatasource"
      >
        {translate('MetadataProvidedBy', { provider: 'TheTVDB' })}
      </Link>
    </div>
  );
}
