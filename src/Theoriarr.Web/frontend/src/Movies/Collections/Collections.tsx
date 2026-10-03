import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { Movie, MovieImage } from 'Movies/Movie';
import MovieGrid from 'Movies/MovieGrid';

interface CollectionResource {
  id: number;
  title: string;
  tmdbId: number;
  overview: string;
  images: MovieImage[];
  movies?: Movie[];
}

function Collections() {
  const { data, isLoading, error } = useApiQuery<CollectionResource[]>({
    service: 'movies',
    path: '/collection',
  });

  const collections = data ?? [];

  const asMovies: Movie[] = collections.map((collection) => ({
    id: collection.id,
    tmdbId: collection.tmdbId,
    title: collection.title,
    sortTitle: collection.title,
    year: 0,
    overview: collection.overview,
    status: '',
    monitored: false,
    hasFile: false,
    movieFileId: 0,
    sizeOnDisk: 0,
    runtime: 0,
    studio: '',
    genres: [],
    certification: '',
    imdbId: '',
    added: '',
    minimumAvailability: '',
    images: collection.images ?? [],
  }));

  return (
    <PageContent title="Collections">
      <PageContentBody>
        {isLoading && collections.length === 0 ? (
          <div style={{ padding: 20 }}>Loading collections...</div>
        ) : null}

        {error ? (
          <div style={{ padding: 20 }}>
            Failed to load collections: {error.message}
          </div>
        ) : null}

        <MovieGrid
          movies={asMovies}
          emptyMessage="No collections found."
          getLink={(movie) => `/movie/collection/${movie.id}`}
          getSubtitle={(movie) =>
            `${collections.find((c) => c.tmdbId === movie.tmdbId)?.movies?.length ?? 0} movies`
          }
        />
      </PageContentBody>
    </PageContent>
  );
}

export default Collections;
