import React from 'react';
import Label from 'Components/Label';
import Modal from 'Components/Modal/Modal';
import { SelectEntityColumn } from 'InteractiveImport/SelectEntity/SelectEntity';
import SelectEntityModalContent from 'InteractiveImport/SelectEntity/SelectEntityModalContent';
import { Movie } from 'Movies/Movie';
import useMovies from 'Movies/useMovies';
import translate from 'Utilities/String/translate';

const columns: SelectEntityColumn<Movie>[] = [
  {
    name: 'title',
    label: () => translate('Title'),
    isVisible: true,
    render: (movie) => movie.title,
  },
  {
    name: 'year',
    label: () => translate('Year'),
    isVisible: true,
    render: (movie) => movie.year,
  },
  {
    name: 'imdbId',
    label: () => translate('ImdbId'),
    isVisible: true,
    render: (movie) => (movie.imdbId ? <Label>{movie.imdbId}</Label> : null),
  },
  {
    name: 'tmdbId',
    label: () => translate('TMDBId'),
    isVisible: true,
    render: (movie) => <Label>{movie.tmdbId}</Label>,
  },
];

const getSearchValues = (movie: Movie) => [
  movie.title,
  movie.tmdbId.toString(),
  movie.imdbId ?? '',
];

interface SelectMovieModalProps {
  isOpen: boolean;
  modalTitle: string;
  onMovieSelect(movie: Movie): void;
  onModalClose(): void;
}

function SelectMovieModal(props: SelectMovieModalProps) {
  const { isOpen, modalTitle, onMovieSelect, onModalClose } = props;
  const { data: movies } = useMovies();

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectEntityModalContent
        title={translate('SelectMovieModalTitle', { modalTitle })}
        filterPlaceholder={translate('FilterMoviePlaceholder')}
        columns={columns}
        items={movies}
        getSearchValues={getSearchValues}
        onSelect={onMovieSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectMovieModal;
