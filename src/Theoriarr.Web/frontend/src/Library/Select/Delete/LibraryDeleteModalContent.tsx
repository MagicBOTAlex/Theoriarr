import React, { useCallback, useMemo, useState } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import { useBulkDeleteMovies } from 'Movies/useMovieMutations';
import { useBulkDeleteSeries } from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import {
  setLibraryDeleteOptions,
  useLibraryDeleteOptions,
} from '../../libraryOptionsStore';
import { splitSelectKeys } from '../../MediaItem';
import useLibraryItems from '../../useLibraryItems';

export interface LibraryDeleteModalContentProps {
  onModalClose(): void;
}

function LibraryDeleteModalContent({
  onModalClose,
}: LibraryDeleteModalContentProps) {
  const { addImportListExclusion, addImportExclusion } =
    useLibraryDeleteOptions();
  const { bulkDeleteSeries, isBulkDeleting } = useBulkDeleteSeries();
  const { bulkDeleteMovies, isBulkDeletingMovies } = useBulkDeleteMovies();
  const { useSelectedIds, unselectAll } = useSelect();
  const selectedIds = useSelectedIds();
  const { items } = useLibraryItems();
  const [deleteFiles, setDeleteFiles] = useState(false);

  const { seriesIds, movieIds } = useMemo(
    () => splitSelectKeys(selectedIds),
    [selectedIds]
  );

  const count = seriesIds.length + movieIds.length;
  const hasMovies = movieIds.length > 0;

  const selectedSize = useMemo(() => {
    return items
      .filter((item) => selectedIds.includes(item.selectKey))
      .reduce((acc, item) => acc + (item.sizeOnDisk ?? 0), 0);
  }, [items, selectedIds]);

  const onDeleteFilesChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setDeleteFiles(value);
    },
    []
  );

  const onDeleteOptionChange = useCallback(
    ({ name, value }: { name: string; value: boolean }) => {
      setLibraryDeleteOptions({ [name]: value });
    },
    []
  );

  const isDeleting = isBulkDeleting || isBulkDeletingMovies;

  const onDeleteConfirmed = useCallback(() => {
    if (isDeleting) {
      return;
    }

    if (seriesIds.length) {
      bulkDeleteSeries({
        seriesIds,
        deleteFiles,
        addImportListExclusion,
      });
    }

    if (movieIds.length) {
      bulkDeleteMovies({
        movieIds,
        deleteFiles,
        addImportExclusion,
      });
    }

    unselectAll();
    setDeleteFiles(false);
    onModalClose();
  }, [
    isDeleting,
    seriesIds,
    movieIds,
    deleteFiles,
    addImportListExclusion,
    addImportExclusion,
    bulkDeleteSeries,
    bulkDeleteMovies,
    unselectAll,
    onModalClose,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteSelectedItems')}</ModalHeader>

      <ModalBody>
        <div>
          <FormGroup>
            <FormLabel>{translate('AddListExclusion')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="addImportListExclusion"
              value={addImportListExclusion}
              helpText={translate('AddListExclusionSeriesHelpText')}
              onChange={onDeleteOptionChange}
            />
          </FormGroup>

          {hasMovies ? (
            <FormGroup>
              <FormLabel>{translate('AddImportExclusion')}</FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="addImportExclusion"
                value={addImportExclusion}
                helpText={translate('AddImportExclusionMovieHelpText')}
                onChange={onDeleteOptionChange}
              />
            </FormGroup>
          ) : null}

          <FormGroup>
            <FormLabel>{translate('DeleteFolders')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="deleteFiles"
              value={deleteFiles}
              helpText={translate('DeleteFoldersHelpText')}
              kind="danger"
              onChange={onDeleteFilesChange}
            />
          </FormGroup>
        </div>

        <div className="mt-5 mb-[10px]">
          {translate('DeleteSelectedItemsConfirmation', { count })}
        </div>

        <div>
          {translate('TotalSize')}: {formatBytes(selectedSize)}
        </div>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button
          kind={kinds.DANGER}
          isDisabled={isDeleting}
          onPress={onDeleteConfirmed}
        >
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default LibraryDeleteModalContent;
