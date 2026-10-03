import React, { useCallback } from 'react';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import translate from 'Utilities/String/translate';
import MovieFileEditorRow from './MovieFileEditorRow';
import MovieFileProvider from './MovieFileProvider';
import useMovieFiles, { useDeleteMovieFiles } from './useMovieFiles';

const columns: Column[] = [
  {
    name: 'relativePath',
    label: () => translate('RelativePath'),
    isVisible: true,
  },
  {
    name: 'videoCodec',
    label: () => translate('VideoCodec'),
    isVisible: true,
  },
  {
    name: 'audioInfo',
    label: () => translate('AudioInfo'),
    isVisible: true,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isVisible: true,
  },
  {
    name: 'releaseGroup',
    label: () => translate('ReleaseGroup'),
    isVisible: true,
  },
  {
    name: 'dateAdded',
    label: () => translate('DateAdded'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isVisible: true,
  },
];

interface MovieFileEditorTableProps {
  movieId: number;
}

function MovieFileEditorTable({ movieId }: MovieFileEditorTableProps) {
  const { data: movieFiles } = useMovieFiles({ movieId });
  const { deleteMovieFiles } = useDeleteMovieFiles();

  const handleDeletePress = useCallback(
    (id: number) => {
      deleteMovieFiles({ movieFileIds: [id] });
    },
    [deleteMovieFiles]
  );

  return (
    <MovieFileProvider movieId={movieId}>
      <div className="border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--inputBackgroundColor)] last:mb-0">
        {movieFiles.length ? null : (
          <div className="pt-[10px] pb-[10px] pl-[2em]">
            {translate('NoMovieFilesToManage')}
          </div>
        )}

        {movieFiles.length ? (
          <Table columns={columns}>
            <TableBody>
              {movieFiles.map((movieFile) => {
                return (
                  <MovieFileEditorRow
                    key={movieFile.id}
                    movieFile={movieFile}
                    onDeletePress={handleDeletePress}
                  />
                );
              })}
            </TableBody>
          </Table>
        ) : null}
      </div>
    </MovieFileProvider>
  );
}

export default MovieFileEditorTable;
