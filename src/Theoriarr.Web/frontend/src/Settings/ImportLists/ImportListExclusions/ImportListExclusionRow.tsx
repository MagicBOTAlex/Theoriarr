import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRow from 'Components/Table/TableRow';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons, kinds } from 'Helpers/Props';
import { useSettingsService } from 'Settings/SettingsServiceContext';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import EditImportListExclusionModal from './EditImportListExclusionModal';
import {
  ImportListExclusion,
  useDeleteImportListExclusion,
} from './useImportListExclusions';

interface ImportListExclusionRowProps extends ImportListExclusion {
  onModalClose: () => void;
}

function ImportListExclusionRow({
  id,
  tvdbId,
  title,
  tmdbId,
  movieTitle,
  movieYear,
  onModalClose,
}: ImportListExclusionRowProps) {
  const service = useSettingsService();
  const isMovies = service === 'movies';

  const { toggleSelected, useIsSelected } = useSelect<ImportListExclusion>();
  const isSelected = useIsSelected(id);

  const { deleteImportListExclusion } = useDeleteImportListExclusion(id);

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  const [
    isEditImportListExclusionModalOpen,
    setEditImportListExclusionModalOpen,
    setEditImportListExclusionModalClosed,
  ] = useModalOpenState(false);

  const handleEditModalClose = useCallback(() => {
    setEditImportListExclusionModalClosed();
    onModalClose();
  }, [setEditImportListExclusionModalClosed, onModalClose]);

  const [
    isDeleteImportListExclusionModalOpen,
    setDeleteImportListExclusionModalOpen,
    setDeleteImportListExclusionModalClosed,
  ] = useModalOpenState(false);

  const handleDeletePress = useCallback(() => {
    deleteImportListExclusion();
  }, [deleteImportListExclusion]);

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      {isMovies ? (
        <>
          <TableRowCell>{movieTitle || title}</TableRowCell>
          <TableRowCell>{movieYear}</TableRowCell>
          <TableRowCell>{tmdbId}</TableRowCell>
        </>
      ) : (
        <>
          <TableRowCell>{title}</TableRowCell>
          <TableRowCell>{tvdbId}</TableRowCell>
        </>
      )}

      <TableRowCell className="w-[35px] whitespace-nowrap">
        <IconButton
          name={icons.EDIT}
          aria-label={translate('Edit')}
          onPress={setEditImportListExclusionModalOpen}
        />
      </TableRowCell>

      <EditImportListExclusionModal
        id={id}
        title={title}
        tvdbId={tvdbId}
        movieTitle={movieTitle}
        tmdbId={tmdbId}
        movieYear={movieYear}
        isOpen={isEditImportListExclusionModalOpen}
        onModalClose={handleEditModalClose}
        onDeleteImportListExclusionPress={setDeleteImportListExclusionModalOpen}
      />

      <ConfirmModal
        isOpen={isDeleteImportListExclusionModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteImportListExclusion')}
        message={translate('DeleteImportListExclusionMessageText')}
        confirmLabel={translate('Delete')}
        onConfirm={handleDeletePress}
        onCancel={setDeleteImportListExclusionModalClosed}
      />
    </TableRow>
  );
}

export default ImportListExclusionRow;
