import classNames from 'classnames';
import React, { useCallback, useEffect, useState } from 'react';
import Alert, { ALERT_CLASS } from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import PageContentBody from 'Components/Page/PageContentBody';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, kinds, sizes } from 'Helpers/Props';
import RootFolders from 'RootFolder/RootFolders';
import useRootFolders, { useAddRootFolder } from 'RootFolder/useRootFolders';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

const ADD_ERROR_ALERT_CLASS = classNames(ALERT_CLASS, 'mx-0 my-[20px]');

function ImportSeriesSelectFolder() {
  const { isFetching, isFetched, error, data } = useRootFolders();
  const { addRootFolder, isAdding, addError } = useAddRootFolder();

  const isWindows = useIsWindows();

  const [isAddNewRootFolderModalOpen, setIsAddNewRootFolderModalOpen] =
    useState(false);

  const wasAdding = usePrevious(isAdding);

  const hasRootFolders = data.length > 0;
  const goodFolderExample = isWindows ? 'C:\\tv shows' : '/tv shows';
  const badFolderExample = isWindows
    ? 'C:\\tv shows\\the simpsons'
    : '/tv shows/the simpsons';

  const handleAddNewRootFolderPress = useCallback(() => {
    setIsAddNewRootFolderModalOpen(true);
  }, []);

  const handleAddRootFolderModalClose = useCallback(() => {
    setIsAddNewRootFolderModalOpen(false);
  }, []);

  const handleNewRootFolderSelect = useCallback(
    ({ value }: InputChanged<string>) => {
      addRootFolder({ path: value, mediaType: 'series' });
    },
    [addRootFolder]
  );

  useEffect(() => {
    if (!isAdding && wasAdding && !addError) {
      data.reduce((acc, item) => {
        if (item.id > acc) {
          return item.id;
        }

        return acc;
      }, 0);
    }
  }, [isAdding, wasAdding, addError, data]);

  return (
    <PageContentBody>
      {isFetching && !isFetched ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <Alert kind={kinds.DANGER}>{translate('RootFoldersLoadError')}</Alert>
      ) : null}

      {!error && isFetched && (
        <div>
          <div className="mb-[40px] text-center font-light text-[36px]">
            {translate('LibraryImportSeriesHeader')}
          </div>

          <div className="text-[20px]">
            {translate('LibraryImportTips')}
            <ul>
              <li className="text-[14px]">
                <InlineMarkdown
                  data={translate('LibraryImportTipsQualityInEpisodeFilename')}
                />
              </li>
              <li className="text-[14px]">
                <InlineMarkdown
                  data={translate('LibraryImportTipsSeriesUseRootFolder', {
                    goodFolderExample,
                    badFolderExample,
                  })}
                />
              </li>
              <li className="text-[14px]">
                {translate('LibraryImportTipsDontUseDownloadsFolder')}
              </li>
            </ul>
          </div>

          {hasRootFolders ? (
            <div className="mt-[40px]">
              <FieldSet legend={translate('RootFolders')}>
                <RootFolders />
              </FieldSet>
            </div>
          ) : null}

          {!isAdding && addError ? (
            <Alert className={ADD_ERROR_ALERT_CLASS} kind={kinds.DANGER}>
              {translate('AddRootFolderError')}

              <ul>
                {Array.isArray(addError.statusBody) ? (
                  addError.statusBody.map((e, index) => {
                    return <li key={index}>{e.errorMessage}</li>;
                  })
                ) : (
                  <li>{JSON.stringify(addError.statusBody)}</li>
                )}
              </ul>
            </Alert>
          ) : null}

          <div className={hasRootFolders ? undefined : 'mt-[40px] text-center'}>
            <Button
              kind={kinds.PRIMARY}
              size={sizes.LARGE}
              onPress={handleAddNewRootFolderPress}
            >
              <Icon className="mr-[8px]" name={icons.DRIVE} />
              {hasRootFolders
                ? translate('ChooseAnotherFolder')
                : translate('StartImport')}
            </Button>
          </div>

          <FileBrowserModal
            isOpen={isAddNewRootFolderModalOpen}
            name="rootFolderPath"
            value=""
            onChange={handleNewRootFolderSelect}
            onModalClose={handleAddRootFolderModalClose}
          />
        </div>
      )}
    </PageContentBody>
  );
}

export default ImportSeriesSelectFolder;
