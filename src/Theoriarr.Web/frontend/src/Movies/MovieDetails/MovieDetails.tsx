import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useNavigate } from 'react-router';
import TextTruncate from 'react-text-truncate';
import CommandNames from 'Commands/CommandNames';
import { useCommands, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import ImdbRating from 'Components/ImdbRating';
import InfoLabel from 'Components/InfoLabel';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import Marquee from 'Components/Marquee';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import RottenTomatoRating from 'Components/RottenTomatoRating';
import TmdbRating from 'Components/TmdbRating';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import TraktRating from 'Components/TraktRating';
import useMeasure from 'Helpers/Hooks/useMeasure';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, kinds, sizes, tooltipPositions } from 'Helpers/Props';
import ExtraFileTable from 'MovieFile/ExtraFileTable';
import MovieFileEditorTable from 'MovieFile/MovieFileEditorTable';
import useMovieFiles from 'MovieFile/useMovieFiles';
import DeleteMovieModal from 'Movies/Delete/DeleteMovieModal';
import EditMovieModal from 'Movies/Edit/EditMovieModal';
import getMovieStatusDetails from 'Movies/getMovieStatusDetails';
import MovieHistoryModal from 'Movies/History/MovieHistoryModal';
import { getMovieFanartUrl, MovieStatus, Statistics } from 'Movies/Movie';
import MovieGenres from 'Movies/MovieGenres';
import MoviePoster from 'Movies/MoviePoster';
import useMovieCredits from 'Movies/useMovieCredits';
import useMovies, {
  useSingleMovie,
  useToggleMovieMonitored,
} from 'Movies/useMovies';
import QualityProfileName from 'Settings/Profiles/Quality/QualityProfileName';
import fonts from 'Styles/Variables/fonts';
import sortByProp from 'Utilities/Array/sortByProp';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import formatBytes from 'Utilities/Number/formatBytes';
import formatRuntime from 'Utilities/Number/formatRuntime';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import translate from 'Utilities/String/translate';
import MovieCastPosters from './Credits/Cast/MovieCastPosters';
import MovieCrewPosters from './Credits/Crew/MovieCrewPosters';
import InteractiveSearchModal from './InteractiveSearchModal';
import MovieDetailsLinks from './MovieDetailsLinks';
import MovieReleaseDates from './MovieReleaseDates';
import MovieStatusLabel from './MovieStatusLabel';
import MovieTags from './MovieTags';
import MovieTitlesTable from './Titles/MovieTitlesTable';

const defaultFontSize = parseInt(fonts.defaultFontSize);
const lineHeight = parseFloat(fonts.lineHeight);

const SEPARATOR_CLASS = 'mr-[14px] max-[1200px]:mr-[9px]';
const DETAIL_TEXT_CLASS = 'font-light text-[17px]';

interface MovieDetailsProps {
  movieId: number;
}

function MovieDetails({ movieId }: MovieDetailsProps) {
  const navigate = useNavigate();
  const executeCommand = useExecuteCommand();

  const movie = useSingleMovie(movieId);
  const { data: allMovies } = useMovies();
  const { toggleMovieMonitored, isTogglingMovieMonitored } =
    useToggleMovieMonitored(movieId);

  const {
    data: movieFiles,
    isFetching: isMovieFilesFetching,
    error: movieFilesError,
    refetch: refetchMovieFiles,
  } = useMovieFiles({ movieId });
  const { isFetching: isMovieCreditsFetching, error: movieCreditsError } =
    useMovieCredits(movieId);

  const hasMovieFiles = movieFiles.length > 0;

  const { data: commands } = useCommands();

  const { isRefreshing, isRenaming, isSearching } = useMemo(() => {
    const movieRefreshingCommand = findCommand(commands, {
      name: CommandNames.RefreshMovie,
    });

    const isMovieRefreshingCommandExecuting = isCommandExecuting(
      movieRefreshingCommand
    );

    const allMoviesRefreshing = !!(
      isMovieRefreshingCommandExecuting &&
      movieRefreshingCommand &&
      (!('movieIds' in movieRefreshingCommand.body) ||
        movieRefreshingCommand.body.movieIds.length === 0)
    );

    const isMovieRefreshing = !!(
      isMovieRefreshingCommandExecuting &&
      movieRefreshingCommand &&
      'movieIds' in movieRefreshingCommand.body &&
      movieRefreshingCommand.body.movieIds?.includes(movieId)
    );

    const isSearchingExecuting = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.MoviesSearch,
        movieIds: [movieId],
      })
    );

    const renamingMovieCommand = findCommand(commands, {
      name: CommandNames.RenameMovie,
    });

    const isRenamingMovie = !!(
      isCommandExecuting(renamingMovieCommand) &&
      renamingMovieCommand &&
      'movieIds' in renamingMovieCommand.body &&
      renamingMovieCommand.body.movieIds?.includes(movieId)
    );

    return {
      isRefreshing: isMovieRefreshing || allMoviesRefreshing,
      isRenaming: isRenamingMovie,
      isSearching: isSearchingExecuting,
    };
  }, [movieId, commands]);

  const { nextMovie, previousMovie } = useMemo(() => {
    const sortedMovies = [...allMovies].sort(sortByProp('sortTitle'));
    const movieIndex = sortedMovies.findIndex((item) => item.id === movieId);

    if (movieIndex === -1) {
      return {
        nextMovie: undefined,
        previousMovie: undefined,
      };
    }

    const next = sortedMovies[movieIndex + 1] ?? sortedMovies[0];
    const previous =
      sortedMovies[movieIndex - 1] ?? sortedMovies[sortedMovies.length - 1];

    return {
      nextMovie: {
        id: next.id,
        title: next.title,
      },
      previousMovie: {
        id: previous.id,
        title: previous.title,
      },
    };
  }, [movieId, allMovies]);

  const touchStart = useRef<number | null>(null);

  const [isInteractiveSearchModalOpen, setIsInteractiveSearchModalOpen] =
    useState(false);
  const [isEditMovieModalOpen, setIsEditMovieModalOpen] = useState(false);
  const [isDeleteMovieModalOpen, setIsDeleteMovieModalOpen] = useState(false);
  const [isMovieHistoryModalOpen, setIsMovieHistoryModalOpen] = useState(false);
  const [titleRef, { width: titleWidth }] = useMeasure();
  const [overviewRef, { height: overviewHeight }] = useMeasure();
  const wasRefreshing = usePrevious(isRefreshing);
  const wasRenaming = usePrevious(isRenaming);

  const handleInteractiveSearchPress = useCallback(() => {
    setIsInteractiveSearchModalOpen(true);
  }, []);

  const handleInteractiveSearchModalClose = useCallback(() => {
    setIsInteractiveSearchModalOpen(false);
  }, []);

  const handleEditMoviePress = useCallback(() => {
    setIsEditMovieModalOpen(true);
  }, []);

  const handleEditMovieModalClose = useCallback(() => {
    setIsEditMovieModalOpen(false);
  }, []);

  const handleDeleteMoviePress = useCallback(() => {
    setIsEditMovieModalOpen(false);
    setIsDeleteMovieModalOpen(true);
  }, []);

  const handleDeleteMovieModalClose = useCallback(() => {
    setIsDeleteMovieModalOpen(false);
  }, []);

  const handleMovieHistoryPress = useCallback(() => {
    setIsMovieHistoryModalOpen(true);
  }, []);

  const handleMovieHistoryModalClose = useCallback(() => {
    setIsMovieHistoryModalOpen(false);
  }, []);

  const handleMonitorTogglePress = useCallback(
    (value: boolean) => {
      toggleMovieMonitored({ monitored: value });
    },
    [toggleMovieMonitored]
  );

  const handleRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshMovie,
      movieIds: [movieId],
    });
  }, [movieId, executeCommand]);

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.MoviesSearch,
      movieIds: [movieId],
    });
  }, [movieId, executeCommand]);

  const handleRenamePress = useCallback(() => {
    executeCommand({
      name: CommandNames.RenameMovie,
      movieIds: [movieId],
    });
  }, [movieId, executeCommand]);

  const populate = useCallback(() => {
    refetchMovieFiles();
  }, [refetchMovieFiles]);

  useEffect(() => {
    populate();
  }, [populate]);

  useEffect(() => {
    registerPagePopulator(populate, ['movieUpdated']);

    return () => {
      unregisterPagePopulator(populate);
    };
  }, [populate]);

  useEffect(() => {
    if ((!isRefreshing && wasRefreshing) || (!isRenaming && wasRenaming)) {
      populate();
    }
  }, [isRefreshing, wasRefreshing, isRenaming, wasRenaming, populate]);

  const handleTouchStart = useCallback((event: TouchEvent) => {
    const touches = event.touches;

    if (touches.length !== 1) {
      return;
    }

    touchStart.current = touches[0].pageX;
  }, []);

  const handleTouchEnd = useCallback(
    (event: TouchEvent) => {
      const touches = event.changedTouches;
      const currentTouch = touches[0].pageX;

      if (!touchStart.current) {
        return;
      }

      if (
        currentTouch > touchStart.current &&
        currentTouch - touchStart.current > 100 &&
        previousMovie !== undefined
      ) {
        navigate(`/movie/${previousMovie.id}`);
      } else if (
        currentTouch < touchStart.current &&
        touchStart.current - currentTouch > 100 &&
        nextMovie !== undefined
      ) {
        navigate(`/movie/${nextMovie.id}`);
      }

      touchStart.current = null;
    },
    [previousMovie, nextMovie, navigate]
  );

  const handleTouchCancel = useCallback(() => {
    touchStart.current = null;
  }, []);

  const handleKeyUp = useCallback(
    (event: KeyboardEvent) => {
      if (
        isInteractiveSearchModalOpen ||
        isEditMovieModalOpen ||
        isDeleteMovieModalOpen ||
        isMovieHistoryModalOpen
      ) {
        return;
      }

      if (event.key === 'ArrowLeft' && previousMovie !== undefined) {
        navigate(`/movie/${previousMovie.id}`);
      }

      if (event.key === 'ArrowRight' && nextMovie !== undefined) {
        navigate(`/movie/${nextMovie.id}`);
      }
    },
    [
      isInteractiveSearchModalOpen,
      isEditMovieModalOpen,
      isDeleteMovieModalOpen,
      isMovieHistoryModalOpen,
      previousMovie,
      nextMovie,
      navigate,
    ]
  );

  useEffect(() => {
    window.addEventListener('touchstart', handleTouchStart);
    window.addEventListener('touchend', handleTouchEnd);
    window.addEventListener('touchcancel', handleTouchCancel);
    window.addEventListener('keyup', handleKeyUp);

    return () => {
      window.removeEventListener('touchstart', handleTouchStart);
      window.removeEventListener('touchend', handleTouchEnd);
      window.removeEventListener('touchcancel', handleTouchCancel);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, [handleTouchStart, handleTouchEnd, handleTouchCancel, handleKeyUp]);

  if (!movie) {
    return null;
  }

  const {
    id,
    tmdbId,
    imdbId,
    title,
    originalTitle,
    year,
    inCinemas,
    physicalRelease,
    digitalRelease,
    runtime,
    certification,
    ratings,
    path,
    statistics = {} as Statistics,
    qualityProfileId,
    monitored,
    studio,
    originalLanguage,
    genres = [],
    collection,
    overview,
    status,
    youTubeTrailerId,
    isAvailable = false,
    images,
    tags = [],
  } = movie;

  const { sizeOnDisk = 0 } = statistics;

  const statusDetails = getMovieStatusDetails(status as MovieStatus);

  const fanartUrl = movie.images ? getMovieFanartUrl(movie) : undefined;

  const allRatings = ratings ?? {};

  const marqueeWidth = Math.max(titleWidth - 150, 0);

  const titleWithYear = `${title}${year > 0 ? ` (${year})` : ''}`;

  const isFetching = isMovieFilesFetching || isMovieCreditsFetching;

  return (
    <PageContent title={titleWithYear}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('RefreshAndScan')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            title={translate('RefreshInformationAndScanDisk')}
            isSpinning={isRefreshing}
            onPress={handleRefreshPress}
          />

          <PageToolbarButton
            label={translate('SearchMovie')}
            iconName={icons.SEARCH}
            isSpinning={isSearching}
            onPress={handleSearchPress}
          />

          <PageToolbarButton
            label={translate('InteractiveSearch')}
            iconName={icons.INTERACTIVE}
            isSpinning={isSearching}
            onPress={handleInteractiveSearchPress}
          />

          <PageToolbarSeparator />

          <PageToolbarButton
            label={translate('Rename')}
            iconName={icons.ORGANIZE}
            isDisabled={!hasMovieFiles}
            isSpinning={isRenaming}
            onPress={handleRenamePress}
          />

          <PageToolbarButton
            label={translate('History')}
            iconName={icons.HISTORY}
            onPress={handleMovieHistoryPress}
          />

          <PageToolbarSeparator />

          <PageToolbarButton
            label={translate('Edit')}
            iconName={icons.EDIT}
            onPress={handleEditMoviePress}
          />

          <PageToolbarButton
            label={translate('Delete')}
            iconName={icons.DELETE}
            onPress={handleDeleteMoviePress}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody innerClassName="p-0">
        <div className="relative w-full h-[425px]">
          <div
            className="absolute z-[-1] w-full h-full bg-cover"
            style={
              fanartUrl ? { backgroundImage: `url(${fanartUrl})` } : undefined
            }
          >
            <div className="absolute w-full h-full bg-[var(--black)] opacity-70" />
          </div>

          <div className="flex p-[30px] w-full h-full text-[var(--white)]">
            <MoviePoster
              className="z-[2] shrink-0 mr-[35px] w-[250px] h-[368px] max-[1200px]:hidden"
              images={images}
              size={500}
              lazy={false}
            />

            <div className="flex flex-col grow overflow-hidden">
              <div
                ref={titleRef}
                className="relative flex justify-between flex-[0_0_auto]"
              >
                <div className="flex mb-[5px]">
                  <div className="self-center mr-[10px]">
                    <MonitorToggleButton
                      className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] w-[40px] hover:text-[var(--iconButtonHoverLightColor)]!"
                      monitored={monitored}
                      isSaving={isTogglingMovieMonitored}
                      size={40}
                      onPress={handleMonitorTogglePress}
                    />
                  </div>

                  <div
                    className="font-light text-[50px] leading-[60px] max-[768px]:text-[30px] max-[768px]:leading-[30px]"
                    style={{ width: marqueeWidth }}
                  >
                    <Marquee text={title} title={originalTitle} />
                  </div>
                </div>

                <div className="absolute right-0 whitespace-nowrap max-[1200px]:hidden">
                  {previousMovie ? (
                    <IconButton
                      className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] ml-[5px] w-[30px] text-[#e1e2e3]! whitespace-nowrap hover:text-[var(--iconButtonHoverLightColor)]!"
                      name={icons.ARROW_LEFT}
                      size={30}
                      title={translate('MovieDetailsGoTo', {
                        title: previousMovie.title,
                      })}
                      to={`/movie/${previousMovie.id}`}
                    />
                  ) : null}

                  {nextMovie ? (
                    <IconButton
                      className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] ml-[5px] w-[30px] text-[#e1e2e3]! whitespace-nowrap hover:text-[var(--iconButtonHoverLightColor)]!"
                      name={icons.ARROW_RIGHT}
                      size={30}
                      title={translate('MovieDetailsGoTo', {
                        title: nextMovie.title,
                      })}
                      to={`/movie/${nextMovie.id}`}
                    />
                  ) : null}
                </div>
              </div>

              <div className="mb-[8px] pl-[7px] font-light text-[20px] max-[1200px]:text-[19px]">
                <div>
                  {certification ? (
                    <span
                      className="mr-[15px] px-[5px] border border-solid rounded-[5px] max-[1200px]:mr-[9px]"
                      title={translate('Certification')}
                    >
                      {certification}
                    </span>
                  ) : null}

                  <span className={SEPARATOR_CLASS}>
                    <Popover
                      anchor={
                        year > 0 ? (
                          year
                        ) : (
                          <Icon
                            name={icons.WARNING}
                            kind={kinds.WARNING}
                            size={20}
                          />
                        )
                      }
                      title={translate('ReleaseDates')}
                      body={
                        <MovieReleaseDates
                          tmdbId={tmdbId}
                          inCinemas={inCinemas}
                          digitalRelease={digitalRelease}
                          physicalRelease={physicalRelease}
                        />
                      }
                      position={tooltipPositions.BOTTOM}
                    />
                  </span>

                  {runtime ? (
                    <span
                      className={SEPARATOR_CLASS}
                      title={translate('Runtime')}
                    >
                      {formatRuntime(runtime)}
                    </span>
                  ) : null}

                  <span className={SEPARATOR_CLASS}>
                    <Tooltip
                      anchor={<Icon name={icons.EXTERNAL_LINK} size={20} />}
                      tooltip={
                        <MovieDetailsLinks
                          tmdbId={tmdbId}
                          imdbId={imdbId}
                          youTubeTrailerId={youTubeTrailerId}
                        />
                      }
                      position={tooltipPositions.BOTTOM}
                    />
                  </span>

                  {tags.length ? (
                    <span>
                      <Tooltip
                        anchor={<Icon name={icons.TAGS} size={20} />}
                        tooltip={<MovieTags movieId={id} />}
                        position={tooltipPositions.BOTTOM}
                      />
                    </span>
                  ) : null}
                </div>
              </div>

              <div className="mb-[8px] pl-[7px] font-light text-[20px] max-[1200px]:text-[19px]">
                {allRatings.tmdb ? (
                  <span className={SEPARATOR_CLASS}>
                    <TmdbRating ratings={allRatings} iconSize={20} />
                  </span>
                ) : null}
                {allRatings.imdb ? (
                  <span className={SEPARATOR_CLASS}>
                    <ImdbRating ratings={allRatings} iconSize={20} />
                  </span>
                ) : null}
                {allRatings.rottenTomatoes ? (
                  <span className={SEPARATOR_CLASS}>
                    <RottenTomatoRating ratings={allRatings} iconSize={20} />
                  </span>
                ) : null}
                {allRatings.trakt ? (
                  <span className={SEPARATOR_CLASS}>
                    <TraktRating ratings={allRatings} iconSize={20} />
                  </span>
                ) : null}
              </div>

              <div>
                <InfoLabel
                  className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                  name={translate('Path')}
                  size={sizes.LARGE}
                >
                  <span className={DETAIL_TEXT_CLASS}>{path}</span>
                </InfoLabel>

                <InfoLabel
                  className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                  name={translate('Status')}
                  title={statusDetails.message}
                  size={sizes.LARGE}
                >
                  <span className={DETAIL_TEXT_CLASS}>
                    <MovieStatusLabel
                      movieId={id}
                      monitored={monitored}
                      isAvailable={isAvailable}
                      hasMovieFiles={hasMovieFiles}
                      status={status as MovieStatus}
                    />
                  </span>
                </InfoLabel>

                <InfoLabel
                  className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                  name={translate('QualityProfile')}
                  size={sizes.LARGE}
                >
                  <span className={DETAIL_TEXT_CLASS}>
                    <QualityProfileName
                      qualityProfileId={qualityProfileId ?? 0}
                    />
                  </span>
                </InfoLabel>

                <InfoLabel
                  className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                  name={translate('Size')}
                  size={sizes.LARGE}
                >
                  <span className={DETAIL_TEXT_CLASS}>
                    {formatBytes(sizeOnDisk)}
                  </span>
                </InfoLabel>

                {collection ? (
                  <InfoLabel
                    className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                    name={translate('Collection')}
                    size={sizes.LARGE}
                  >
                    <div className={DETAIL_TEXT_CLASS}>
                      <Link to={`/movie/collection/${collection.tmdbId}`}>
                        {collection.title}
                      </Link>
                    </div>
                  </InfoLabel>
                ) : null}

                {originalLanguage?.name ? (
                  <InfoLabel
                    className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                    name={translate('OriginalLanguage')}
                    size={sizes.LARGE}
                  >
                    <span className={DETAIL_TEXT_CLASS}>
                      {originalLanguage.name}
                    </span>
                  </InfoLabel>
                ) : null}

                {studio ? (
                  <InfoLabel
                    className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                    name={translate('Studio')}
                    size={sizes.LARGE}
                  >
                    <span className={DETAIL_TEXT_CLASS}>{studio}</span>
                  </InfoLabel>
                ) : null}

                {genres.length ? (
                  <InfoLabel
                    className="inline-block m-[2px] whitespace-nowrap leading-none cursor-default text-inherit my-[5px] mr-[10px] ml-0"
                    name={translate('Genres')}
                    size={sizes.LARGE}
                  >
                    <MovieGenres
                      className={DETAIL_TEXT_CLASS}
                      genres={genres}
                    />
                  </InfoLabel>
                ) : null}
              </div>

              <div
                ref={overviewRef}
                className="flex-[1_0_0] mt-[8px] min-h-0 text-balance text-[15px]"
              >
                <TextTruncate
                  line={Math.max(
                    Math.floor(overviewHeight / (defaultFontSize * lineHeight)),
                    1
                  )}
                  text={overview}
                />
              </div>
            </div>
          </div>
        </div>

        <div className="p-[20px] max-[768px]:px-0">
          {!isFetching && movieFilesError ? (
            <Alert kind={kinds.DANGER}>
              {translate('LoadingMovieFilesFailed')}
            </Alert>
          ) : null}

          {!isFetching && movieCreditsError ? (
            <Alert kind={kinds.DANGER}>
              {translate('LoadingMovieCreditsFailed')}
            </Alert>
          ) : null}

          <FieldSet legend={translate('Files')}>
            <MovieFileEditorTable movieId={id} />

            <ExtraFileTable movieId={id} />
          </FieldSet>

          <FieldSet legend={translate('Cast')}>
            <MovieCastPosters movieId={id} />
          </FieldSet>

          <FieldSet legend={translate('Crew')}>
            <MovieCrewPosters movieId={id} />
          </FieldSet>

          <FieldSet legend={translate('Titles')}>
            <MovieTitlesTable movieId={id} />
          </FieldSet>
        </div>

        <EditMovieModal
          isOpen={isEditMovieModalOpen}
          movieId={id}
          onModalClose={handleEditMovieModalClose}
          onDeleteMoviePress={handleDeleteMoviePress}
        />

        <MovieHistoryModal
          isOpen={isMovieHistoryModalOpen}
          movieId={id}
          onModalClose={handleMovieHistoryModalClose}
        />

        <DeleteMovieModal
          isOpen={isDeleteMovieModalOpen}
          movieId={id}
          onModalClose={handleDeleteMovieModalClose}
        />

        <InteractiveSearchModal
          isOpen={isInteractiveSearchModalOpen}
          movieId={id}
          movieTitle={title}
          onModalClose={handleInteractiveSearchModalClose}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default MovieDetails;
