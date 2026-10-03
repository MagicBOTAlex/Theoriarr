import React, { useCallback, useState } from 'react';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds } from 'Helpers/Props';
import CompressModal from 'Library/Compression/CompressModal';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import MediaInfo from './MediaInfo';
import { MovieFile } from './MovieFile';
import MovieFileLanguages from './MovieFileLanguages';

interface MovieFileEditorRowProps {
  movieFile: MovieFile;
  onDeletePress: (id: number) => void;
}

function MovieFileEditorRow({
  movieFile,
  onDeletePress,
}: MovieFileEditorRowProps) {
  const { id, relativePath, size, releaseGroup, languages, dateAdded } =
    movieFile;

  const [isConfirmDeleteModalOpen, setIsConfirmDeleteModalOpen] =
    useState(false);
  const [isCompressModalOpen, setIsCompressModalOpen] = useState(false);

  const handleCompressPress = useCallback(() => {
    setIsCompressModalOpen(true);
  }, []);

  const handleCompressModalClose = useCallback(() => {
    setIsCompressModalOpen(false);
  }, []);

  const handleDeletePress = useCallback(() => {
    setIsConfirmDeleteModalOpen(true);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    setIsConfirmDeleteModalOpen(false);
    onDeletePress(id);
  }, [id, onDeletePress]);

  const handleConfirmDeleteModalClose = useCallback(() => {
    setIsConfirmDeleteModalOpen(false);
  }, []);

  return (
    <TableRow>
      <TableRowCell title={relativePath} className="break-all">
        {relativePath}
      </TableRowCell>

      <TableRowCell className="w-[100px]">
        <MediaInfo movieFileId={id} type="video" />
      </TableRowCell>

      <TableRowCell className="w-[100px]">
        <MediaInfo movieFileId={id} type="audio" />
      </TableRowCell>

      <TableRowCell title={String(size)} className="w-[100px]">
        {formatBytes(size)}
      </TableRowCell>

      <TableRowCell className="w-[100px]">
        <MovieFileLanguages languages={languages} />
      </TableRowCell>

      <TableRowCell className="w-[100px]">{releaseGroup}</TableRowCell>

      <RelativeDateCell className="w-[100px]" date={dateAdded} />

      <TableRowCell className="w-[100px]">
        <IconButton
          title={translate('CompressMedia')}
          name={icons.GPU}
          onPress={handleCompressPress}
        />

        <IconButton
          title={translate('DeleteFile')}
          name={icons.REMOVE}
          onPress={handleDeletePress}
        />
      </TableRowCell>

      <CompressModal
        isOpen={isCompressModalOpen}
        movieFileIds={[id]}
        onModalClose={handleCompressModalClose}
      />

      <ConfirmModal
        isOpen={isConfirmDeleteModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteSelectedMovieFiles')}
        message={translate('DeleteSelectedMovieFilesHelpText')}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDelete}
        onCancel={handleConfirmDeleteModalClose}
      />
    </TableRow>
  );
}

export default MovieFileEditorRow;
