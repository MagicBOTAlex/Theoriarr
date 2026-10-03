import React from 'react';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

interface NoSeriesProps {
  totalItems: number;
}

function NoSeries(props: NoSeriesProps) {
  const { totalItems } = props;

  if (totalItems > 0) {
    return (
      <div>
        <div className="mt-[10px] mb-[30px] text-center text-[20px]">
          {translate('AllSeriesAreHiddenByTheAppliedFilter')}
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className="mt-[10px] mb-[30px] text-center text-[20px]">
        {translate('NoSeriesFoundImportOrAdd')}
      </div>

      <div className="mt-5 text-center">
        <Button to="/add/import" kind={kinds.PRIMARY}>
          {translate('ImportExistingSeries')}
        </Button>
      </div>

      <div className="mt-5 text-center">
        <Button to="/add/new" kind={kinds.PRIMARY}>
          {translate('AddNewSeries')}
        </Button>
      </div>
    </div>
  );
}

export default NoSeries;
