import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { MovieImage } from 'Movies/Movie';
import getMediaCoverUrl from 'Utilities/MediaCover';
import translate from 'Utilities/String/translate';

interface DiscoverMovieResource {
  tmdbId: number;
  title: string;
  year: number;
  overview?: string;
  status?: string;
  studio?: string;
  runtime?: number;
  images?: MovieImage[];
  isExisting: boolean;
  isExcluded: boolean;
  isRecommendation: boolean;
  isTrending: boolean;
  isPopular: boolean;
}

interface RootFolderOption {
  path: string;
}

interface QualityProfileOption {
  id: number;
  name: string;
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

interface ExcludeMoviePayload {
  tmdbId: number;
  movieTitle: string;
  movieYear: number;
}

const OPTION_LABEL_CLASS =
  'flex flex-col gap-1 text-[12px] font-medium text-[var(--disabledColor)]';

const OPTION_SELECT_CLASS =
  'rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] px-2 py-[5px] text-[13px] text-[var(--textColor)]';

const OPTION_CHECKBOX_CLASS = 'h-4 w-4 accent-[var(--themeBlue)]';

const OPTION_CHECK_LABEL_CLASS =
  'flex items-center gap-2 pb-[7px] text-[12px] font-medium text-[var(--textColor)]';

const MINIMUM_AVAILABILITY_OPTIONS = [
  { value: 'announced', labelKey: 'Announced' },
  { value: 'inCinemas', labelKey: 'InCinemas' },
  { value: 'released', labelKey: 'Released' },
];

interface DiscoverMovieItemProps {
  movie: DiscoverMovieResource;
  isAdding: boolean;
  onAdd: (movie: DiscoverMovieResource) => void;
  onExclude: (movie: DiscoverMovieResource) => void;
}

function DiscoverMovieItem({
  movie,
  isAdding,
  onAdd,
  onExclude,
}: DiscoverMovieItemProps) {
  const handleAddPress = useCallback(() => {
    onAdd(movie);
  }, [movie, onAdd]);

  const handleExcludePress = useCallback(() => {
    onExclude(movie);
  }, [movie, onExclude]);

  const image =
    movie.images?.find((item) => item.coverType === 'poster') ??
    movie.images?.[0];
  const posterUrl = image
    ? getMediaCoverUrl('movies', image.remoteUrl || image.url)
    : undefined;

  return (
    <div className="flex flex-col">
      <div className="relative aspect-[2/3] overflow-hidden rounded bg-[var(--cardBackgroundColor)]">
        {posterUrl ? (
          <img
            className="h-full w-full object-cover"
            src={posterUrl}
            alt={movie.title}
            loading="lazy"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center p-2 text-center text-[12px] text-[var(--disabledColor)]">
            {movie.title}
          </div>
        )}

        {movie.isExisting ? (
          <span className="absolute top-[6px] right-[6px] rounded-[3px] bg-[var(--successColor)] px-[6px] py-[2px] text-[10px] font-bold uppercase text-[var(--white)]">
            In Library
          </span>
        ) : null}

        {!movie.isExisting && movie.isExcluded ? (
          <span
            className={`${'absolute top-[6px] right-[6px] rounded-[3px] bg-[var(--successColor)] px-[6px] py-[2px] text-[10px] font-bold uppercase text-[var(--white)]'} ${'bg-[var(--dangerColor)]'}`}
          >
            Excluded
          </span>
        ) : null}
      </div>

      <span className="mt-[6px] overflow-hidden text-ellipsis whitespace-nowrap text-[13px] font-semibold text-[var(--textColor)]">
        {movie.title}
      </span>
      <span className="text-[12px] text-[var(--disabledColor)]">
        {movie.year}
        {movie.studio ? ` \u00b7 ${movie.studio}` : ''}
      </span>

      {!movie.isExisting && !movie.isExcluded ? (
        <div className="mt-[6px] flex gap-[6px]">
          <button
            className="flex-1 cursor-pointer rounded-[3px] border border-[var(--defaultBorderColor)] bg-[var(--buttonBackgroundColor,transparent)] px-2 py-1 text-[12px] text-[var(--textColor)] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50"
            type="button"
            disabled={isAdding}
            onClick={handleAddPress}
          >
            Add
          </button>

          <button
            className={`${'flex-1 cursor-pointer rounded-[3px] border border-[var(--defaultBorderColor)] bg-[var(--buttonBackgroundColor,transparent)] px-2 py-1 text-[12px] text-[var(--textColor)] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50'} ${'border-[var(--dangerColor)] text-[var(--dangerColor)]'}`}
            type="button"
            onClick={handleExcludePress}
          >
            Exclude
          </button>
        </div>
      ) : null}
    </div>
  );
}

function DiscoverMovie() {
  const queryClient = useQueryClient();
  const [message, setMessage] = useState('');
  const [qualityProfileId, setQualityProfileId] = useState<number | null>(null);
  const [rootFolderPath, setRootFolderPath] = useState('');
  const [monitored, setMonitored] = useState(true);
  const [minimumAvailability, setMinimumAvailability] = useState('released');
  const [searchForMovie, setSearchForMovie] = useState(true);

  const { data, isLoading, error } = useApiQuery<DiscoverMovieResource[]>({
    service: 'movies',
    path: '/importlist/movie',
  });

  const { data: rootFolders } = useApiQuery<RootFolderOption[]>({
    service: 'movies',
    path: '/rootfolder',
  });

  const { data: qualityProfiles } = useApiQuery<QualityProfileOption[]>({
    service: 'movies',
    path: '/qualityprofile',
  });

  useEffect(() => {
    if (!rootFolderPath && rootFolders?.[0]?.path) {
      setRootFolderPath(rootFolders[0].path);
    }
  }, [rootFolderPath, rootFolders]);

  useEffect(() => {
    if (qualityProfileId == null && qualityProfiles?.[0]?.id != null) {
      setQualityProfileId(qualityProfiles[0].id);
    }
  }, [qualityProfileId, qualityProfiles]);

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
        queryClient.invalidateQueries({ queryKey: ['movies', '/movie'] });
        queryClient.invalidateQueries({
          queryKey: ['movies', '/importlist/movie'],
        });
      },
      onError: (addError) => {
        setMessage(`Failed to add movie: ${addError.message}`);
      },
    },
  });

  const { mutate: excludeMovie } = useApiMutation<unknown, ExcludeMoviePayload>(
    {
      service: 'movies',
      path: '/exclusions',
      method: 'POST',
      mutationOptions: {
        onSuccess: () => {
          setMessage('Movie excluded');
          queryClient.invalidateQueries({
            queryKey: ['movies', '/importlist/movie'],
          });
        },
        onError: (excludeError) => {
          setMessage(`Failed to exclude movie: ${excludeError.message}`);
        },
      },
    }
  );

  const handleQualityProfileChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setQualityProfileId(Number(event.target.value));
    },
    []
  );

  const handleRootFolderChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setRootFolderPath(event.target.value);
    },
    []
  );

  const handleMinimumAvailabilityChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setMinimumAvailability(event.target.value);
    },
    []
  );

  const handleMonitoredChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setMonitored(event.target.checked);
    },
    []
  );

  const handleSearchForMovieChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setSearchForMovie(event.target.checked);
    },
    []
  );

  const handleAddPress = useCallback(
    (movie: DiscoverMovieResource) => {
      if (!rootFolderPath || qualityProfileId == null) {
        setMessage(
          'Add a root folder and quality profile in Movies settings first.'
        );
        return;
      }

      setMessage('');
      addMovie({
        tmdbId: movie.tmdbId,
        title: movie.title,
        year: movie.year,
        qualityProfileId,
        rootFolderPath,
        monitored,
        minimumAvailability,
        addOptions: { searchForMovie },
      });
    },
    [
      addMovie,
      minimumAvailability,
      monitored,
      qualityProfileId,
      rootFolderPath,
      searchForMovie,
    ]
  );

  const handleExcludePress = useCallback(
    (movie: DiscoverMovieResource) => {
      setMessage('');
      excludeMovie({
        tmdbId: movie.tmdbId,
        movieTitle: movie.title,
        movieYear: movie.year,
      });
    },
    [excludeMovie]
  );

  const movies = data ?? [];

  return (
    <PageContent title="Discover">
      <PageContentBody>
        <div className="p-3">
          <div className="mb-3 flex flex-wrap items-end gap-x-5 gap-y-3">
            <label className={OPTION_LABEL_CLASS}>
              {translate('QualityProfile')}
              <select
                className={OPTION_SELECT_CLASS}
                value={qualityProfileId ?? ''}
                onChange={handleQualityProfileChange}
              >
                {(qualityProfiles ?? []).map((profile) => (
                  <option key={profile.id} value={profile.id}>
                    {profile.name}
                  </option>
                ))}
              </select>
            </label>

            <label className={OPTION_LABEL_CLASS}>
              {translate('RootFolder')}
              <select
                className={OPTION_SELECT_CLASS}
                value={rootFolderPath}
                onChange={handleRootFolderChange}
              >
                {(rootFolders ?? []).map((rootFolder) => (
                  <option key={rootFolder.path} value={rootFolder.path}>
                    {rootFolder.path}
                  </option>
                ))}
              </select>
            </label>

            <label className={OPTION_LABEL_CLASS}>
              {translate('MinimumAvailability')}
              <select
                className={OPTION_SELECT_CLASS}
                value={minimumAvailability}
                onChange={handleMinimumAvailabilityChange}
              >
                {MINIMUM_AVAILABILITY_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {translate(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>

            <label className={OPTION_CHECK_LABEL_CLASS}>
              <input
                className={OPTION_CHECKBOX_CLASS}
                type="checkbox"
                checked={monitored}
                onChange={handleMonitoredChange}
              />
              {translate('Monitored')}
            </label>

            <label className={OPTION_CHECK_LABEL_CLASS}>
              <input
                className={OPTION_CHECKBOX_CLASS}
                type="checkbox"
                checked={searchForMovie}
                onChange={handleSearchForMovieChange}
              />
              {translate('SearchOnAdd')}
            </label>
          </div>

          {message ? (
            <div
              className={`${'mb-3 text-[13px] text-[var(--successColor)]'} ${
                message.startsWith('Failed') ? 'text-[var(--dangerColor)]' : ''
              }`}
            >
              {message}
            </div>
          ) : null}

          {isLoading && movies.length === 0 ? (
            <div className="mb-3 text-[13px] text-[var(--successColor)]">
              Loading Discover...
            </div>
          ) : null}

          {error ? (
            <div
              className={`${'mb-3 text-[13px] text-[var(--successColor)]'} ${'text-[var(--dangerColor)]'}`}
            >
              Failed to load Discover: {error.message}
            </div>
          ) : null}

          {!isLoading && !error && movies.length === 0 ? (
            <div className="mb-3 text-[13px] text-[var(--successColor)]">
              No recommendations found. Configure import lists in Movies
              settings.
            </div>
          ) : null}

          <div className="grid grid-cols-[repeat(auto-fill,minmax(150px,1fr))] gap-4">
            {movies.map((movie) => (
              <DiscoverMovieItem
                key={movie.tmdbId}
                movie={movie}
                isAdding={isAdding}
                onAdd={handleAddPress}
                onExclude={handleExcludePress}
              />
            ))}
          </div>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default DiscoverMovie;
