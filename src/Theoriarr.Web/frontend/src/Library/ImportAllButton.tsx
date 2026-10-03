import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import useImportAllMedia from './useImportAllMedia';

function ImportAllButton() {
  const { importAll, isImporting } = useImportAllMedia();

  const handlePress = useCallback(() => {
    importAll();
  }, [importAll]);

  return (
    <SpinnerButton
      kind={kinds.DEFAULT}
      isSpinning={isImporting}
      onPress={handlePress}
    >
      <Icon className="mr-[6px]" name={icons.REFRESH} />
      {translate('ImportAll')}
    </SpinnerButton>
  );
}

export default ImportAllButton;
