import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebounce } from 'use-debounce';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { getMoviePosterUrl, Movie } from 'Movies/Movie';
import translate from 'Utilities/String/translate';

interface RootFolder {
  id: number;
  path: string;
}

interface QualityProfile {
  id: number;
  name: string;
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

interface AddMovieResultProps {
  movie: Movie;
  onAdd: (movie: Movie) => void;
}

function AddMovieResult({ movie, onAdd }: AddMovieResultProps) {
  const posterUrl = getMoviePosterUrl(movie);

  const handleAdd = useCallback(() => {
    onAdd(movie);
  }, [onAdd, movie]);

  return (
    <div className="flex gap-3 rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] p-[10px]">
      {posterUrl ? (
        <img
          className="h-[105px] w-[70px] shrink-0 grow-0 basis-[70px] rounded-[3px] bg-[var(--pageBackground)] object-cover"
          src={posterUrl}
          alt={movie.title}
          loading="lazy"
        />
      ) : (
        <div className="h-[105px] w-[70px] shrink-0 grow-0 basis-[70px] rounded-[3px] bg-[var(--pageBackground)] object-cover" />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <span className="text-[14px] font-semibold text-[var(--textColor)]">
          {movie.title}
        </span>
        <span className="text-[12px] text-[var(--disabledColor)]">
          {movie.year}
        </span>
        {movie.overview ? (
          <span className="mt-1 flex-1 overflow-hidden [display:-webkit-box] [-webkit-line-clamp:3] [-webkit-box-orient:vertical] text-[12px] leading-[1.4] text-[var(--disabledColor)]">
            {movie.overview}
          </span>
        ) : null}

        <button
          className="mt-2 self-start rounded bg-[var(--themeBlue)] px-[14px] py-[5px] text-[12px] font-medium text-[var(--white)] hover:bg-[var(--themeAlternateBlue)] hover:text-[var(--white)]"
          type="button"
          onClick={handleAdd}
        >
          Add
        </button>
      </div>
    </div>
  );
}

function AddNewMovie() {
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const [term, setTerm] = useState(searchParams.get('term') ?? '');
  const [debouncedTerm] = useDebounce(term, 400);
  const [message, setMessage] = useState('');
  const [qualityProfileId, setQualityProfileId] = useState<number | null>(null);
  const [rootFolderPath, setRootFolderPath] = useState(
    searchParams.get('rootFolder') ?? ''
  );
  const [monitored, setMonitored] = useState(true);
  const [minimumAvailability, setMinimumAvailability] = useState('released');
  const [searchForMovie, setSearchForMovie] = useState(true);

  const { data: rootFolders } = useApiQuery<RootFolder[]>({
    service: 'movies',
    path: '/rootfolder',
  });

  const { data: qualityProfiles } = useApiQuery<QualityProfile[]>({
    service: 'movies',
    path: '/qualityprofile',
  });

  const { data: results, isLoading } = useApiQuery<Movie[]>({
    service: 'movies',
    path: '/movie/lookup',
    queryParams: { term: debouncedTerm },
    queryOptions: {
      enabled: debouncedTerm.trim().length > 2,
    },
  });

  const paramTerm = searchParams.get('term') ?? '';

  useEffect(() => {
    if (paramTerm) {
      setTerm(paramTerm);
    }
  }, [paramTerm]);

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

  const addMovie = useApiMutation<Movie, Record<string, unknown>>({
    service: 'movies',
    method: 'POST',
    path: '/movie',
    mutationOptions: {
      onSuccess: (movie) => {
        setMessage(`Added ${movie.title}`);
        queryClient.invalidateQueries({ queryKey: ['movies', '/movie'] });
        queryClient.invalidateQueries({
          queryKey: ['movies', '/movie/lookup'],
        });
      },
      onError: (error) => {
        setMessage(`Failed to add movie: ${error.message}`);
      },
    },
  });

  const handleTermChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setTerm(event.target.value);
    },
    []
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

  const handleAdd = useCallback(
    (movie: Movie) => {
      if (!rootFolderPath || qualityProfileId == null) {
        setMessage(
          'Add a root folder and quality profile in Movies settings first.'
        );
        return;
      }

      setMessage('');

      addMovie.mutate({
        title: movie.title,
        tmdbId: movie.tmdbId,
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

  return (
    <PageContentBody>
      <div className="flex h-full flex-col">
        <div className="border-b border-[var(--defaultBorderColor)] px-5 py-4">
          <input
            className="w-full max-w-[520px] rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] px-3 py-[9px] text-[14px] text-[var(--textColor)]"
            type="text"
            value={term}
            placeholder="Search for a movie (e.g. The Matrix)"
            onChange={handleTermChange}
          />
        </div>

        <div className="flex flex-wrap items-end gap-x-5 gap-y-3 border-b border-[var(--defaultBorderColor)] px-5 py-4">
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
                <option key={rootFolder.id} value={rootFolder.path}>
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
          <div className="p-5 text-[var(--disabledColor)]">{message}</div>
        ) : null}

        {isLoading && results == null ? (
          <div className="p-5 text-[var(--disabledColor)]">Searching...</div>
        ) : null}

        <div className="grid grid-cols-[repeat(auto-fill,minmax(320px,1fr))] gap-[14px] p-5">
          {(results ?? []).map((movie) => (
            <AddMovieResult
              key={movie.tmdbId}
              movie={movie}
              onAdd={handleAdd}
            />
          ))}
        </div>
      </div>
    </PageContentBody>
  );
}

export default AddNewMovie;
