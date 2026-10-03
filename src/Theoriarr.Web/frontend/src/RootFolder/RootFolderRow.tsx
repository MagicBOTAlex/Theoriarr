import React, { useCallback, useState } from 'react';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import {
  RootFolder,
  serviceForMediaType,
  useDeleteRootFolder,
} from './useRootFolders';

type RootFolderRowProps = RootFolder & {
  disableImportLink?: boolean;
};

const MEDIA_TYPE_LABELS: Record<string, string> = {
  series: 'TV',
  anime: 'Anime',
  movie: 'Movies',
};

function RootFolderRow(props: RootFolderRowProps) {
  const {
    id,
    path,
    mediaType,
    accessible,
    isEmpty,
    freeSpace = 0,
    unmappedFolders = [],
    disableImportLink = false,
  } = props;

  const isUnavailable = !accessible;
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const { deleteRootFolder } = useDeleteRootFolder(
    id,
    serviceForMediaType(mediaType)
  );

  // Series import only exists for the series domain (TV/Anime); movie folders
  // have no `/add/import` target.
  const canImport =
    !isUnavailable && !disableImportLink && mediaType !== 'movie';

  const onDeletePress = useCallback(() => {
    setIsDeleteModalOpen(true);
  }, [setIsDeleteModalOpen]);

  const onDeleteModalClose = useCallback(() => {
    setIsDeleteModalOpen(false);
  }, [setIsDeleteModalOpen]);

  const onConfirmDelete = useCallback(() => {
    deleteRootFolder();
    setIsDeleteModalOpen(false);
  }, [deleteRootFolder]);

  return (
    <TableRow>
      <TableRowCell>{MEDIA_TYPE_LABELS[mediaType] ?? mediaType}</TableRowCell>

      <TableRowCell>
        <div className="flex items-center">
          {canImport ? <Link to={`/add/import/${id}`}>{path}</Link> : path}

          {isUnavailable ? (
            <Label
              className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px]"
              kind={kinds.DANGER}
            >
              {translate('Unavailable')}
            </Label>
          ) : null}

          {accessible && isEmpty ? (
            <Label
              className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px]"
              kind={kinds.WARNING}
              title={translate('EmptyRootFolderTooltip')}
            >
              {translate('Empty')}
            </Label>
          ) : null}
        </div>
      </TableRowCell>

      <TableRowCell className="w-[150px]">
        {isUnavailable || isNaN(Number(freeSpace))
          ? '-'
          : formatBytes(freeSpace)}
      </TableRowCell>

      <TableRowCell className="w-[150px]">
        {isUnavailable ? '-' : unmappedFolders.length}
      </TableRowCell>

      <TableRowCell className="w-[45px]">
        <IconButton
          title={translate('RemoveRootFolder')}
          aria-label={translate('RemoveRootFolder')}
          name={icons.REMOVE}
          onPress={onDeletePress}
        />
      </TableRowCell>

      <ConfirmModal
        isOpen={isDeleteModalOpen}
        kind={kinds.DANGER}
        title={translate('RemoveRootFolder')}
        message={translate('RemoveRootFolderWithSeriesMessageText', { path })}
        confirmLabel={translate('Remove')}
        onConfirm={onConfirmDelete}
        onCancel={onDeleteModalClose}
      />
    </TableRow>
  );
}

export default RootFolderRow;
