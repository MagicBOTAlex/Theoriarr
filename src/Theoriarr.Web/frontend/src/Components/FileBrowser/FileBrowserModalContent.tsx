import classNames from 'classnames';
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import Alert, { ALERT_CLASS } from 'Components/Alert';
import { PATH_INPUT_WRAPPER_CLASS } from 'Components/Form/InputClassNames';
import { PathInputInternal } from 'Components/Form/PathInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ModalBody, { MODAL_BODY_CLASS } from 'Components/Modal/ModalBody';
import ModalContent, {
  MODAL_CONTENT_CLASS,
} from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Scroller from 'Components/Scroller/Scroller';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { kinds, scrollDirections } from 'Helpers/Props';
import usePaths from 'Path/usePaths';
import { useSystemStatusData } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import FileBrowserRow from './FileBrowserRow';

const FILE_BROWSER_BODY_CLASS = classNames(MODAL_BODY_CLASS, 'flex flex-col');

const FILE_BROWSER_CONTENT_CLASS = classNames(
  MODAL_CONTENT_CLASS,
  'bg-[color-mix(in_srgb,var(--modalBackgroundColor),black_8%)]!'
);

const PATH_INPUT_CLASS = classNames(
  PATH_INPUT_WRAPPER_CLASS,
  'flex-[0_0_auto]'
);

const MAPPED_DRIVES_WARNING_CLASS = classNames(ALERT_CLASS, 'm-0! mb-[20px]');

const baseColumns: Column[] = [
  {
    name: 'type',
    label: () => translate('Type'),
    isVisible: true,
  },
  {
    name: 'name',
    label: () => translate('Name'),
    isVisible: true,
  },
];

const handleClearPaths = () => {};

export interface FileBrowserModalContentProps {
  name: string;
  value: string;
  includeFiles?: boolean;
  hideOkButton?: boolean;
  onChange: (args: InputChanged<string>) => unknown;
  onModalClose: () => void;
  renderDirectoryActions?: (directory: {
    name: string;
    path: string;
  }) => React.ReactNode;
}

function FileBrowserModalContent({
  name,
  value,
  includeFiles = true,
  hideOkButton = false,
  onChange,
  onModalClose,
  renderDirectoryActions,
}: FileBrowserModalContentProps) {
  const [currentPath, setCurrentPath] = useState(value);
  const scrollerRef = useRef(null);
  const previousValue = usePrevious(value);
  const { isWindows, mode } = useSystemStatusData();

  const columns = useMemo(() => {
    if (!renderDirectoryActions) {
      return baseColumns;
    }

    return [
      ...baseColumns,
      {
        name: 'actions',
        label: () => '',
        isVisible: true,
      },
    ];
  }, [renderDirectoryActions]);

  const isActionsColumnVisible = !!renderDirectoryActions;

  const { isFetching, isFetched, error, data } = usePaths({
    path: currentPath,
    allowFoldersWithoutTrailingSlashes: true,
    includeFiles,
  });

  const { directories, files, parent, paths } = data;

  const emptyParent = parent === '';
  const isWindowsService = isWindows && mode === 'service';

  const handlePathInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setCurrentPath(value);
    },
    []
  );

  const handleRowPress = useCallback((path: string) => {
    setCurrentPath(path);
  }, []);

  const handleOkPress = useCallback(() => {
    onChange({
      name,
      value: currentPath,
    });

    onModalClose();
  }, [name, currentPath, onChange, onModalClose]);

  const handleFetchPaths = useCallback((path: string) => {
    setCurrentPath(path);
  }, []);

  useEffect(() => {
    if (value !== previousValue && value !== currentPath) {
      setCurrentPath(value);
    }
  }, [value, previousValue, currentPath, setCurrentPath]);

  return (
    <ModalContent
      className={FILE_BROWSER_CONTENT_CLASS}
      onModalClose={onModalClose}
    >
      <ModalHeader>{translate('FileBrowser')}</ModalHeader>

      <ModalBody
        className={FILE_BROWSER_BODY_CLASS}
        scrollDirection={scrollDirections.NONE}
      >
        {isWindowsService ? (
          <Alert className={MAPPED_DRIVES_WARNING_CLASS} kind={kinds.WARNING}>
            <InlineMarkdown
              data={translate('MappedNetworkDrivesWindowsService', {
                url: 'https://wiki.servarr.com/sonarr/faq#why-cant-sonarr-see-my-files-on-a-remote-server',
              })}
            />
          </Alert>
        ) : null}

        <PathInputInternal
          className={PATH_INPUT_CLASS}
          placeholder={translate('FileBrowserPlaceholderText')}
          hasFileBrowser={false}
          includeFiles={includeFiles}
          paths={paths}
          name={name}
          value={currentPath}
          onChange={handlePathInputChange}
          onFetchPaths={handleFetchPaths}
          onClearPaths={handleClearPaths}
        />

        <Scroller
          ref={scrollerRef}
          className="mt-[20px]"
          scrollDirection="both"
        >
          {error ? (
            <Alert kind={kinds.DANGER}>
              {translate('ErrorLoadingContents')}
            </Alert>
          ) : null}

          {isFetched && !error ? (
            <Table horizontalScroll={false} columns={columns}>
              <TableBody>
                {emptyParent ? (
                  <FileBrowserRow
                    type="computer"
                    name={translate('MyComputer')}
                    path={parent}
                    isActionsColumnVisible={isActionsColumnVisible}
                    onPress={handleRowPress}
                  />
                ) : null}

                {!emptyParent && parent ? (
                  <FileBrowserRow
                    type="parent"
                    name="..."
                    path={parent}
                    isActionsColumnVisible={isActionsColumnVisible}
                    onPress={handleRowPress}
                  />
                ) : null}

                {directories.map((directory) => {
                  return (
                    <FileBrowserRow
                      key={directory.path}
                      type={directory.type}
                      name={directory.name}
                      path={directory.path}
                      actions={renderDirectoryActions?.(directory)}
                      isActionsColumnVisible={isActionsColumnVisible}
                      onPress={handleRowPress}
                    />
                  );
                })}

                {files.map((file) => {
                  return (
                    <FileBrowserRow
                      key={file.path}
                      type={file.type}
                      name={file.name}
                      path={file.path}
                      isActionsColumnVisible={isActionsColumnVisible}
                      onPress={handleRowPress}
                    />
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
        </Scroller>
      </ModalBody>

      <ModalFooter>
        {isFetching ? (
          <LoadingIndicator className="inline-block mr-auto" size={20} />
        ) : null}

        <Button onPress={onModalClose}>{translate('Close')}</Button>

        {hideOkButton ? null : (
          <Button onPress={handleOkPress}>{translate('Ok')}</Button>
        )}
      </ModalFooter>
    </ModalContent>
  );
}

export default FileBrowserModalContent;
