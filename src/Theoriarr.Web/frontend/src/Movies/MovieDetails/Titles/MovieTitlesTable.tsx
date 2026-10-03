import React, { useMemo } from 'react';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { useSingleMovie } from 'Movies/useMovies';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import MovieTitlesRow from './MovieTitlesRow';

const columns: Column[] = [
  {
    name: 'alternativeTitle',
    label: () => translate('AlternativeTitle'),
    isVisible: true,
  },
  {
    name: 'sourceType',
    label: () => translate('Type'),
    isVisible: true,
  },
];

interface MovieTitlesTableProps {
  movieId: number;
}

function MovieTitlesTable({ movieId }: MovieTitlesTableProps) {
  const movie = useSingleMovie(movieId);

  const items = useMemo(() => {
    return [...(movie?.alternateTitles ?? [])].sort(sortByProp('title'));
  }, [movie]);

  return (
    <div className="rounded border border-[var(--borderColor)] bg-[var(--inputBackgroundColor)] last:mb-0">
      {items.length ? null : (
        <div className="py-[10px] pl-[2em]">
          {translate('NoAlternativeTitles')}
        </div>
      )}

      {items.length ? (
        <Table columns={columns}>
          <TableBody>
            {items.map((item) => {
              return (
                <MovieTitlesRow
                  key={`${item.title}-${item.sourceType}`}
                  title={item.title}
                  sourceType={item.sourceType}
                />
              );
            })}
          </TableBody>
        </Table>
      ) : null}
    </div>
  );
}

export default MovieTitlesTable;
