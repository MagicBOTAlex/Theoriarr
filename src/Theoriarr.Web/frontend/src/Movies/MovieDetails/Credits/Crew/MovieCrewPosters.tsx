import React, { useMemo } from 'react';
import useMovieCredits from 'Movies/useMovieCredits';
import MovieCreditPosters from '../MovieCreditPosters';

interface MovieCrewPostersProps {
  movieId: number;
}

function MovieCrewPosters({ movieId }: MovieCrewPostersProps) {
  const { data } = useMovieCredits(movieId);

  const crewCredits = useMemo(() => {
    return data.filter((credit) => credit.type === 'crew');
  }, [data]);

  return <MovieCreditPosters items={crewCredits} subtitleKey="job" />;
}

export default MovieCrewPosters;
