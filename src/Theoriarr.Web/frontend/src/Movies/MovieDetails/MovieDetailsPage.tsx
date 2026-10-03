import React, { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import NotFound from 'Components/NotFound';
import usePrevious from 'Helpers/Hooks/usePrevious';
import useMovies from 'Movies/useMovies';
import translate from 'Utilities/String/translate';
import MovieDetails from './MovieDetails';

function MovieDetailsPage() {
  const { data: allMovies } = useMovies();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const movieId = Number(id);

  const movieIndex = allMovies.findIndex((movie) => movie.id === movieId);

  const previousIndex = usePrevious(movieIndex);

  useEffect(() => {
    if (
      movieIndex === -1 &&
      previousIndex !== -1 &&
      previousIndex !== undefined
    ) {
      navigate('/movie');
    }
  }, [movieIndex, previousIndex, navigate]);

  if (movieIndex === -1) {
    return <NotFound message={translate('MovieCannotBeFound')} />;
  }

  return <MovieDetails movieId={movieId} />;
}

export default MovieDetailsPage;
