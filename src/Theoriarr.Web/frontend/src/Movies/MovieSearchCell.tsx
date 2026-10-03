import React, { useCallback } from 'react';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import InteractiveSearchModal from './MovieDetails/InteractiveSearchModal';

interface MovieSearchCellProps {
  movieId: number;
  movieTitle: string;
}

function MovieSearchCell({ movieId, movieTitle }: MovieSearchCellProps) {
  const isSearching = useCommandExecuting('MoviesSearch', {
    movieIds: [movieId],
  });

  const executeCommand = useExecuteCommand();

  const [isSearchModalOpen, setSearchModalOpen, setSearchModalClosed] =
    useModalOpenState(false);

  const handleSearchPress = useCallback(() => {
    executeCommand({ name: 'MoviesSearch', movieIds: [movieId] });
  }, [movieId, executeCommand]);

  return (
    <TableRowCell>
      <SpinnerIconButton
        name={icons.SEARCH}
        isSpinning={isSearching}
        title={translate('AutomaticSearch')}
        onPress={handleSearchPress}
      />

      <IconButton
        name={icons.INTERACTIVE}
        title={translate('InteractiveSearch')}
        aria-label={translate('InteractiveSearch')}
        onPress={setSearchModalOpen}
      />

      <InteractiveSearchModal
        isOpen={isSearchModalOpen}
        movieId={movieId}
        movieTitle={movieTitle}
        onModalClose={setSearchModalClosed}
      />
    </TableRowCell>
  );
}

export default MovieSearchCell;
