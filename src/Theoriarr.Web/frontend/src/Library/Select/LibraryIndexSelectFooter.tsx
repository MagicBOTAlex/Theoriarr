import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import SpinnerButton from 'Components/Link/SpinnerButton';
import PageContentFooter, {
  PAGE_CONTENT_FOOTER_CLASS,
} from 'Components/Page/PageContentFooter';
import { kinds } from 'Helpers/Props';
import CompressModal from 'Library/Compression/CompressModal';
import { useSaveMovieEditor } from 'Movies/useMovieMutations';
import { useSaveSeriesEditor, useUpdateSeriesMonitor } from 'Series/useSeries';
import translate from 'Utilities/String/translate';
import { splitSelectKeys } from '../MediaItem';
import DeleteSeriesFilesModal from './Delete/Files/DeleteSeriesFilesModal';
import LibraryDeleteModal from './Delete/LibraryDeleteModal';
import LibraryEditModal from './Edit/LibraryEditModal';
import { LibraryEditSavePayload } from './Edit/LibraryEditModalContent';
import ChangeMonitoringModal from './SeasonPass/ChangeMonitoringModal';
import LibraryTagsModal from './Tags/LibraryTagsModal';

const FOOTER_CLASS = classNames(
  PAGE_CONTENT_FOOTER_CLASS,
  'items-center max-[768px]:flex max-[768px]:flex-col'
);
const BUTTONS_CLASS =
  'flex max-[992px]:justify-center max-[992px]:w-full max-[768px]:flex-col max-[768px]:mt-[20px] max-[768px]:gap-[20px]';
const ACTION_BUTTONS_CLASS =
  'flex gap-[10px] max-[768px]:flex-wrap max-[768px]:justify-center';
const DELETE_BUTTONS_CLASS =
  'flex gap-[10px] ml-[50px] max-[768px]:ml-0 max-[768px]:justify-center';
const SELECTED_CLASS =
  'flex grow justify-end font-bold max-[992px]:justify-center max-[992px]:mb-[20px] max-[992px]:w-full max-[992px]:order-[-1] max-[768px]:justify-center max-[768px]:order-[-1]';

function LibraryIndexSelectFooter() {
  const { saveSeriesEditor, isSavingSeriesEditor } = useSaveSeriesEditor();
  const { updateSeriesMonitor, isUpdatingSeriesMonitor } =
    useUpdateSeriesMonitor();
  const { saveMovieEditor, isSavingMovieEditor } = useSaveMovieEditor();
  const executeCommand = useExecuteCommand();
  const isDeleteFilesCommandExecuting = useCommandExecuting(
    CommandNames.DeleteSeriesFiles
  );

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isTagsModalOpen, setIsTagsModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isDeleteFilesModalOpen, setIsDeleteFilesModalOpen] = useState(false);
  const [isMonitoringModalOpen, setIsMonitoringModalOpen] = useState(false);
  const [isCompressModalOpen, setIsCompressModalOpen] = useState(false);
  const [isSavingSeries, setIsSavingSeries] = useState(false);
  const [isSavingMovies, setIsSavingMovies] = useState(false);
  const [isSavingMonitoring, setIsSavingMonitoring] = useState(false);
  const [isRenaming, setIsRenaming] = useState(false);
  const [isSearching, setIsSearching] = useState(false);

  const { selectedCount, useSelectedIds } = useSelect();
  const selectedIds = useSelectedIds();

  const { seriesIds, movieIds } = useMemo(
    () => splitSelectKeys(selectedIds),
    [selectedIds]
  );

  const isSaving =
    isSavingSeriesEditor || isSavingMovieEditor || isUpdatingSeriesMonitor;
  const anySelected = selectedCount > 0;
  const hasSeriesSelected = seriesIds.length > 0;

  const onEditPress = useCallback(() => setIsEditModalOpen(true), []);
  const onEditModalClose = useCallback(() => setIsEditModalOpen(false), []);

  const onSavePress = useCallback(
    (payload: LibraryEditSavePayload) => {
      setIsSavingSeries(true);
      setIsSavingMovies(true);
      setIsEditModalOpen(false);

      if (seriesIds.length) {
        saveSeriesEditor({ ...payload, seriesIds });
      }

      if (movieIds.length) {
        saveMovieEditor({
          ...payload,
          movieIds,
          applyTags: payload.tags ? 'replace' : undefined,
        });
      }
    },
    [seriesIds, movieIds, saveSeriesEditor, saveMovieEditor]
  );

  const onOrganizePress = useCallback(() => {
    setIsRenaming(true);

    if (seriesIds.length) {
      executeCommand({ name: CommandNames.RenameSeries, seriesIds });
    }

    if (movieIds.length) {
      executeCommand({ name: CommandNames.RenameMovie, movieIds });
    }

    setTimeout(() => setIsRenaming(false), 1000);
  }, [seriesIds, movieIds, executeCommand]);

  const onSearchPress = useCallback(() => {
    setIsSearching(true);

    if (movieIds.length) {
      executeCommand({ name: CommandNames.MoviesSearch, movieIds });
    }

    seriesIds.forEach((seriesId) => {
      executeCommand({ name: CommandNames.SeriesSearch, seriesId });
    });

    setTimeout(() => setIsSearching(false), 1000);
  }, [seriesIds, movieIds, executeCommand]);

  const onCompressPress = useCallback(() => setIsCompressModalOpen(true), []);
  const onCompressModalClose = useCallback(
    () => setIsCompressModalOpen(false),
    []
  );

  const onTagsPress = useCallback(() => setIsTagsModalOpen(true), []);
  const onTagsModalClose = useCallback(() => setIsTagsModalOpen(false), []);

  const onApplyTagsPress = useCallback(
    (tags: number[]) => {
      setIsSavingSeries(true);
      setIsSavingMovies(true);
      setIsTagsModalOpen(false);

      if (seriesIds.length) {
        saveSeriesEditor({ seriesIds, tags });
      }

      if (movieIds.length) {
        saveMovieEditor({ movieIds, tags, applyTags: 'replace' });
      }
    },
    [seriesIds, movieIds, saveSeriesEditor, saveMovieEditor]
  );

  const onDeletePress = useCallback(() => setIsDeleteModalOpen(true), []);
  const onDeleteModalClose = useCallback(() => setIsDeleteModalOpen(false), []);

  const onDeleteFilesPress = useCallback(
    () => setIsDeleteFilesModalOpen(true),
    []
  );
  const onDeleteFilesModalClose = useCallback(
    () => setIsDeleteFilesModalOpen(false),
    []
  );

  const onMonitoringPress = useCallback(
    () => setIsMonitoringModalOpen(true),
    []
  );
  const onMonitoringModalClose = useCallback(
    () => setIsMonitoringModalOpen(false),
    []
  );

  const onMonitoringSavePress = useCallback(
    (monitor: string) => {
      setIsSavingMonitoring(true);
      setIsMonitoringModalOpen(false);

      if (seriesIds.length) {
        updateSeriesMonitor({
          series: seriesIds.map((id) => ({ id })),
          monitoringOptions: { monitor },
        });
      }
    },
    [seriesIds, updateSeriesMonitor]
  );

  React.useEffect(() => {
    if (!isSaving) {
      setIsSavingSeries(false);
      setIsSavingMovies(false);
      setIsSavingMonitoring(false);
    }
  }, [isSaving]);

  return (
    <PageContentFooter className={FOOTER_CLASS}>
      <div className={BUTTONS_CLASS}>
        <div className={ACTION_BUTTONS_CLASS}>
          <SpinnerButton
            kind={kinds.PRIMARY}
            isSpinning={isSearching}
            isDisabled={!anySelected}
            onPress={onSearchPress}
          >
            {translate('SearchSelected')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isSaving && (isSavingSeries || isSavingMovies)}
            isDisabled={!anySelected}
            onPress={onEditPress}
          >
            {translate('Edit')}
          </SpinnerButton>

          <SpinnerButton
            kind={kinds.WARNING}
            isSpinning={isRenaming}
            isDisabled={!anySelected}
            onPress={onOrganizePress}
          >
            {translate('RenameFiles')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={false}
            isDisabled={!anySelected}
            onPress={onCompressPress}
          >
            {translate('CompressMedia')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isSaving && (isSavingSeries || isSavingMovies)}
            isDisabled={!anySelected}
            onPress={onTagsPress}
          >
            {translate('SetTags')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isSaving && isSavingMonitoring}
            isDisabled={!hasSeriesSelected}
            onPress={onMonitoringPress}
          >
            {translate('UpdateMonitoring')}
          </SpinnerButton>
        </div>

        <div className={DELETE_BUTTONS_CLASS}>
          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={false}
            isDisabled={!anySelected}
            onPress={onDeletePress}
          >
            {translate('Delete')}
          </SpinnerButton>

          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={isDeleteFilesCommandExecuting}
            isDisabled={!hasSeriesSelected}
            onPress={onDeleteFilesPress}
          >
            {translate('DeleteFiles')}
          </SpinnerButton>
        </div>
      </div>

      <div className={SELECTED_CLASS}>
        {translate('CountSelected', { count: selectedCount })}
      </div>

      <LibraryEditModal
        isOpen={isEditModalOpen}
        selectedCount={selectedCount}
        onSavePress={onSavePress}
        onModalClose={onEditModalClose}
      />

      <LibraryTagsModal
        isOpen={isTagsModalOpen}
        onApplyTagsPress={onApplyTagsPress}
        onModalClose={onTagsModalClose}
      />

      <LibraryDeleteModal
        isOpen={isDeleteModalOpen}
        onModalClose={onDeleteModalClose}
      />

      <DeleteSeriesFilesModal
        isOpen={isDeleteFilesModalOpen}
        onModalClose={onDeleteFilesModalClose}
      />

      <ChangeMonitoringModal
        isOpen={isMonitoringModalOpen}
        onSavePress={onMonitoringSavePress}
        onModalClose={onMonitoringModalClose}
      />

      <CompressModal
        isOpen={isCompressModalOpen}
        seriesIds={seriesIds}
        movieIds={movieIds}
        onModalClose={onCompressModalClose}
      />
    </PageContentFooter>
  );
}

export default LibraryIndexSelectFooter;
