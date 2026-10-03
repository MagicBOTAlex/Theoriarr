import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import MenuItem from 'Components/Menu/MenuItem';
import { icons, kinds, sizes } from 'Helpers/Props';
import useRootFolders, {
  RootFolder,
  RootFolderMediaType,
  serviceForMediaType,
  useAddRootFolder,
  useUpdateRootFolder,
} from 'RootFolder/useRootFolders';
import translate from 'Utilities/String/translate';

const MEDIA_TYPE_OPTIONS: { value: RootFolderMediaType; label: string }[] = [
  { value: 'series', label: 'TV' },
  { value: 'movie', label: 'Movies' },
  { value: 'anime', label: 'Anime' },
];

const MEDIA_TYPE_LABELS: Record<RootFolderMediaType, string> = {
  series: 'TV',
  movie: 'Movies',
  anime: 'Anime',
};

// Root folder paths are stored without a trailing separator; the file browser
// hands out directory paths with one.
function normalizePath(path: string) {
  return path.replace(/\\/g, '/').replace(/\/+$/, '');
}

interface SetRootFolderTypeMenuItemProps {
  mediaType: RootFolderMediaType;
  isDisabled?: boolean;
  onSetType: (mediaType: RootFolderMediaType) => void;
  children: React.ReactNode;
}

function SetRootFolderTypeMenuItem({
  mediaType,
  isDisabled = false,
  onSetType,
  children,
}: SetRootFolderTypeMenuItemProps) {
  const handlePress = useCallback(() => {
    onSetType(mediaType);
  }, [mediaType, onSetType]);

  return (
    <MenuItem isDisabled={isDisabled} onPress={handlePress}>
      {children}
    </MenuItem>
  );
}

interface DirectoryActionsProps {
  directory: { name: string; path: string };
  existing?: RootFolder;
  isAdding: boolean;
  onAdd: (
    directory: { name: string; path: string },
    mediaType: RootFolderMediaType
  ) => void;
}

function DirectoryActions({
  directory,
  existing,
  isAdding,
  onAdd,
}: DirectoryActionsProps) {
  const { updateRootFolder, isUpdating } = useUpdateRootFolder(
    existing?.id ?? 0,
    existing ? serviceForMediaType(existing.mediaType) : 'series'
  );

  const handleSetType = useCallback(
    (mediaType: RootFolderMediaType) => {
      if (existing) {
        if (existing.mediaType !== mediaType) {
          updateRootFolder({ path: existing.path, mediaType });
        }
      } else {
        onAdd(directory, mediaType);
      }
    },
    [directory, existing, onAdd, updateRootFolder]
  );

  return (
    <div className="flex items-center justify-end gap-[8px]">
      {existing ? (
        <Label kind={kinds.SUCCESS} title={translate('RootFolderAlreadyAdded')}>
          {MEDIA_TYPE_LABELS[existing.mediaType] ?? existing.mediaType}
        </Label>
      ) : null}

      <Menu alignMenu="right">
        <MenuButton isDisabled={isAdding || isUpdating}>
          {existing ? translate('Change') : translate('Add')}
        </MenuButton>

        <MenuContent>
          {MEDIA_TYPE_OPTIONS.map((option) => {
            return (
              <SetRootFolderTypeMenuItem
                key={option.value}
                mediaType={option.value}
                isDisabled={existing?.mediaType === option.value}
                onSetType={handleSetType}
              >
                {existing
                  ? translate('ChangeRootFolderTo', { type: option.label })
                  : translate('AddRootFolderWithType', {
                      type: option.label,
                    })}
              </SetRootFolderTypeMenuItem>
            );
          })}
        </MenuContent>
      </Menu>
    </div>
  );
}

function AddRootFolder() {
  const { addRootFolder, isAdding, addError } = useAddRootFolder();
  const { data: rootFolders } = useRootFolders(true);

  const [isAddNewRootFolderModalOpen, setIsAddNewRootFolderModalOpen] =
    useState(false);

  const rootFolderByPath = useMemo(() => {
    const map = new Map<string, RootFolder>();

    rootFolders.forEach((rootFolder) => {
      map.set(normalizePath(rootFolder.path), rootFolder);
    });

    return map;
  }, [rootFolders]);

  const onAddNewRootFolderPress = useCallback(() => {
    setIsAddNewRootFolderModalOpen(true);
  }, []);

  const onAddRootFolderModalClose = useCallback(() => {
    setIsAddNewRootFolderModalOpen(false);
  }, []);

  // Folders are added through each row's Add menu; the browser has no OK action.
  const onFolderBrowserChange = useCallback(() => {}, []);

  const onAddDirectory = useCallback(
    (
      directory: { name: string; path: string },
      mediaType: RootFolderMediaType
    ) => {
      addRootFolder({ path: directory.path, mediaType });
    },
    [addRootFolder]
  );

  const renderDirectoryActions = useCallback(
    (directory: { name: string; path: string }) => (
      <DirectoryActions
        directory={directory}
        existing={rootFolderByPath.get(normalizePath(directory.path))}
        isAdding={isAdding}
        onAdd={onAddDirectory}
      />
    ),
    [isAdding, onAddDirectory, rootFolderByPath]
  );

  return (
    <>
      {!isAdding && addError ? (
        <Alert kind={kinds.DANGER}>
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

      <div className="mt-5">
        <Button
          kind={kinds.PRIMARY}
          size={sizes.LARGE}
          onPress={onAddNewRootFolderPress}
        >
          <Icon className="mr-2" name={icons.DRIVE} />
          {translate('AddRootFolder')}
        </Button>
      </div>

      <FileBrowserModal
        isOpen={isAddNewRootFolderModalOpen}
        name="rootFolderPath"
        value=""
        includeFiles={false}
        hideOkButton={true}
        renderDirectoryActions={renderDirectoryActions}
        onChange={onFolderBrowserChange}
        onModalClose={onAddRootFolderModalClose}
      />
    </>
  );
}

export default AddRootFolder;
