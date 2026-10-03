import React, { useMemo } from 'react';
import useMovieCredits from 'Movies/useMovieCredits';
import MovieCreditPosters from '../MovieCreditPosters';

interface MovieCastPostersProps {
  movieId: number;
}

function MovieCastPosters({ movieId }: MovieCastPostersProps) {
  const { data } = useMovieCredits(movieId);

  const castCredits = useMemo(() => {
    return data.filter((credit) => credit.type === 'cast');
  }, [data]);

  return <MovieCreditPosters items={castCredits} subtitleKey="character" />;
}

export default MovieCastPosters;
