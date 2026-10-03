import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { Movie, MovieImage } from 'Movies/Movie';
import MovieGrid from 'Movies/MovieGrid';
import getMediaCoverUrl from 'Utilities/MediaCover';

interface CollectionMovieResource {
  tmdbId: number;
  imdbId?: string;
  title: string;
  sortTitle?: string;
  status?: string;
  overview?: string;
  runtime?: number;
  images?: MovieImage[];
  year?: number;
  genres?: string[];
  folder?: string;
  isExisting: boolean;
  isExcluded: boolean;
}

interface CollectionResource {
  id: number;
  title: string;
  sortTitle?: string;
  tmdbId: number;
  images?: MovieImage[];
  overview?: string;
  monitored: boolean;
  rootFolderPath?: string;
  qualityProfileId: number;
  searchOnAdd: boolean;
  minimumAvailability?: string;
  movies: CollectionMovieResource[];
  missingMovies?: number;
  tags?: number[];
}

interface AddMoviePayload {
  tmdbId: number;
  title: string;
  year?: number;
  qualityProfileId: number;
  rootFolderPath: string;
  monitored: boolean;
  minimumAvailability: string;
  addOptions: {
    searchForMovie: boolean;
  };
}

function CollectionDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const collectionId = Number(id);
  const queryClient = useQueryClient();

  const [message, setMessage] = useState('');
  const [monitored, setMonitored] = useState(false);
  const [searchOnAdd, setSearchOnAdd] = useState(false);

  const { data, isLoading, error } = useApiQuery<CollectionResource>({
    service: 'movies',
    path: `/collection/${collectionId}`,
  });

  useEffect(() => {
    if (!data) {
      return;
    }

    setMonitored(data.monitored);
    setSearchOnAdd(data.searchOnAdd);
  }, [data]);

  const { mutate: saveCollection, isPending: isSaving } = useApiMutation<
    CollectionResource,
    CollectionResource
  >({
    service: 'movies',
    path: '/collection',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (updated) => {
        queryClient.setQueryData(
          ['movies', `/collection/${collectionId}`],
          updated
        );
        setMessage('Saved');
      },
      onError: (saveError) => {
        setMessage(`Failed to save: ${saveError.message}`);
      },
    },
  });

  const { mutate: addMovie, isPending: isAdding } = useApiMutation<
    unknown,
    AddMoviePayload
  >({
    service: 'movies',
    path: '/movie',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setMessage('Movie added');
        queryClient.invalidateQueries({
          queryKey: ['movies', `/collection/${collectionId}`],
        });
        queryClient.invalidateQueries({ queryKey: ['movies', '/movie'] });
      },
      onError: (addError) => {
        setMessage(`Failed to add movie: ${addError.message}`);
      },
    },
  });

  const handleSavePress = useCallback(() => {
    if (!data) {
      return;
    }

    setMessage('');
    saveCollection({ ...data, monitored, searchOnAdd });
  }, [data, monitored, searchOnAdd, saveCollection]);

  const handleAddMoviePress = useCallback(
    (movie: CollectionMovieResource) => {
      if (!data?.rootFolderPath || !data.qualityProfileId) {
        setMessage(
          'Set a root folder and quality profile on this collection first.'
        );
        return;
      }

      setMessage('');
      addMovie({
        tmdbId: movie.tmdbId,
        title: movie.title,
        year: movie.year,
        qualityProfileId: data.qualityProfileId,
        rootFolderPath: data.rootFolderPath,
        monitored: data.monitored,
        minimumAvailability: data.minimumAvailability ?? 'released',
        addOptions: { searchForMovie: data.searchOnAdd },
      });
    },
    [data, addMovie]
  );

  if (isLoading || !data) {
    return (
      <PageContent title="Collection">
        <PageContentBody>
          <div
            className={
              error
                ? 'p-6 text-[var(--dangerColor)]'
                : 'p-6 text-[var(--disabledColor)]'
            }
          >
            {error
              ? `Failed to load collection: ${error.message}`
              : 'Loading...'}
          </div>
        </PageContentBody>
      </PageContent>
    );
  }

  const posterImage =
    data.images?.find((image) => image.coverType === 'poster') ??
    data.images?.[0];
  const posterUrl = posterImage
    ? getMediaCoverUrl('movies', posterImage.remoteUrl || posterImage.url)
    : undefined;

  const missingMovies = data.movies.filter(
    (movie) => !movie.isExisting && !movie.isExcluded
  );

  const asMovies: Movie[] = data.movies.map((movie) => ({
    id: movie.tmdbId,
    tmdbId: movie.tmdbId,
    title: movie.title,
    sortTitle: movie.sortTitle ?? movie.title,
    year: movie.year ?? 0,
    overview: movie.overview ?? '',
    status: movie.status ?? '',
    monitored: false,
    hasFile: movie.isExisting,
    movieFileId: 0,
    sizeOnDisk: 0,
    runtime: movie.runtime ?? 0,
    studio: '',
    genres: movie.genres ?? [],
    certification: '',
    imdbId: movie.imdbId ?? '',
    added: '',
    minimumAvailability: '',
    images: movie.images ?? [],
  }));

  return (
    <PageContent title={data.title}>
      <PageContentBody>
        <div className="flex gap-6 p-6">
          <div className="shrink-0 grow-0 basis-[220px]">
            {posterUrl ? (
              <img
                className="w-[220px] rounded border border-[var(--defaultBorderColor)]"
                src={posterUrl}
                alt={data.title}
              />
            ) : (
              <div className="flex h-[330px] w-[220px] items-center justify-center rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] p-3 text-center text-[var(--disabledColor)]">
                {data.title}
              </div>
            )}
          </div>

          <div className="min-w-0 flex-1">
            <Link
              className="mb-4 inline-block text-[var(--linkColor)]"
              to="/movie/collections"
            >
              &larr; Back to Collections
            </Link>

            <h1 className="mb-1 text-[24px] font-semibold text-[var(--textColor)]">
              {data.title}
            </h1>

            <div className="mb-4 flex flex-wrap gap-[14px] text-[13px] text-[var(--disabledColor)]">
              <span>{data.movies.length} movies</span>
              {data.missingMovies != null ? (
                <span>{data.missingMovies} missing</span>
              ) : null}
            </div>

            <div className="mb-4 flex flex-wrap gap-2">
              <button
                className="cursor-pointer rounded-[3px] border border-[var(--defaultBorderColor)] bg-[var(--buttonBackgroundColor,transparent)] px-3 py-[6px] text-[13px] text-[var(--textColor)] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50"
                type="button"
                disabled={isSaving}
                onClick={handleSavePress}
              >
                {isSaving ? 'Saving...' : 'Save'}
              </button>
            </div>

            {message ? (
              <div
                className={`${'mb-3 text-[13px] text-[var(--successColor)]'} ${
                  message.startsWith('Failed')
                    ? 'text-[var(--dangerColor)]'
                    : ''
                }`}
              >
                {message}
              </div>
            ) : null}

            {data.overview ? (
              <p className="mt-4 leading-[1.5] text-[var(--textColor)]">
                {data.overview}
              </p>
            ) : null}

            <div className="mb-6 flex max-w-[520px] flex-col gap-3 rounded border border-[var(--defaultBorderColor)] p-4">
              <label className="flex items-center gap-2 text-[13px]">
                <input
                  type="checkbox"
                  checked={monitored}
                  onChange={(event) => setMonitored(event.target.checked)}
                />
                Monitored
              </label>

              <label className="flex items-center gap-2 text-[13px]">
                <input
                  type="checkbox"
                  checked={searchOnAdd}
                  onChange={(event) => setSearchOnAdd(event.target.checked)}
                />
                Search on Add
              </label>
            </div>

            <div className="mt-6">
              <h2 className="mb-3 text-[16px] font-semibold text-[var(--textColor)]">
                Movies
              </h2>

              <MovieGrid
                movies={asMovies}
                emptyMessage="This collection has no movies."
                getLink={() => '/movie/collections'}
                getSubtitle={(movie) => {
                  const collectionMovie = data.movies.find(
                    (item) => item.tmdbId === movie.tmdbId
                  );

                  if (!collectionMovie) {
                    return '';
                  }

                  if (collectionMovie.isExisting) {
                    return 'In Library';
                  }

                  if (collectionMovie.isExcluded) {
                    return 'Excluded';
                  }

                  return 'Missing';
                }}
              />

              {missingMovies.length ? (
                <div className="mb-4 flex flex-wrap gap-2">
                  {missingMovies.map((movie) => (
                    <button
                      key={movie.tmdbId}
                      className="cursor-pointer rounded-[3px] border border-[var(--defaultBorderColor)] bg-[var(--buttonBackgroundColor,transparent)] px-3 py-[6px] text-[13px] text-[var(--textColor)] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50"
                      type="button"
                      disabled={isAdding}
                      onClick={() => handleAddMoviePress(movie)}
                    >
                      Add {movie.title}
                    </button>
                  ))}
                </div>
              ) : null}
            </div>
          </div>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default CollectionDetailsPage;
