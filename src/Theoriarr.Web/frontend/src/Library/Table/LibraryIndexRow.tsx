import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import CheckInput from 'Components/Form/CheckInput';
import HeartRating from 'Components/HeartRating';
import ImdbRating from 'Components/ImdbRating';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import MovieTagList from 'Components/MovieTagList';
import RottenTomatoRating from 'Components/RottenTomatoRating';
import SeriesTagList from 'Components/SeriesTagList';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import Column from 'Components/Table/Column';
import TmdbRating from 'Components/TmdbRating';
import TraktRating from 'Components/TraktRating';
import getReleaseTypeName from 'Episode/getReleaseTypeName';
import { icons } from 'Helpers/Props';
import useCountryName from 'Internationalization/useCountryName';
import DeleteMovieModal from 'Movies/Delete/DeleteMovieModal';
import EditMovieModal from 'Movies/Edit/EditMovieModal';
import MovieTitleLink from 'Movies/MovieTitleLink';
import DeleteSeriesModal from 'Series/Delete/DeleteSeriesModal';
import EditSeriesModal from 'Series/Edit/EditSeriesModal';
import SeriesBanner from 'Series/SeriesBanner';
import SeriesTitleLink from 'Series/SeriesTitleLink';
import { SelectStateInputProps } from 'typings/props';
import formatBytes from 'Utilities/Number/formatBytes';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import { useLibraryTableOptions } from '../libraryOptionsStore';
import { MediaItem } from '../MediaItem';
import LibraryIndexProgressBar from '../ProgressBar/LibraryIndexProgressBar';
import useLibraryIndexItem from '../useLibraryIndexItem';
import hasGrowableColumns from './hasGrowableColumns';
import LibraryStatusCell from './LibraryStatusCell';

const COLUMN_CLASSES: Record<string, string> = {
  status: 'flex items-center flex-[0_0_60px]',
  sortTitle: 'flex items-center flex-[4_0_110px]',
  seriesType: 'flex items-center flex-[0_0_100px]',
  network: 'flex items-center flex-[2_0_90px]',
  originalCountry: 'flex items-center flex-[1_0_125px]',
  originalLanguage: 'flex items-center flex-[1_0_125px]',
  qualityProfileId: 'flex items-center flex-[1_0_125px]',
  releaseGroups: 'flex items-center flex-[0_0_180px]',
  releaseTypes: 'flex items-center flex-[0_0_180px]',
  nextAiring: 'flex items-center flex-[0_0_180px]',
  previousAiring: 'flex items-center flex-[0_0_180px]',
  added: 'flex items-center flex-[0_0_180px]',
  genres: 'flex items-center flex-[0_0_180px]',
  episodeFileQualities: 'flex items-center flex-[0_0_220px]',
  seasonCount: 'flex items-center flex-[0_0_100px]',
  certification: 'flex items-center flex-[0_0_100px]',
  seasonFolder: 'flex items-center flex-[0_0_150px]',
  episodeProgress: 'flex items-center justify-center flex-[0_0_150px] flex-col',
  latestSeason: 'flex items-center justify-center flex-[0_0_150px] flex-col',
  episodeCount: 'flex items-center flex-[0_0_130px]',
  year: 'flex items-center flex-[0_0_80px]',
  path: 'flex items-center flex-[1_0_150px]',
  sizeOnDisk: 'flex items-center flex-[0_0_120px]',
  averageSizePerEpisode: 'flex items-center flex-[0_0_160px]',
  ratings: 'flex items-center flex-[0_0_80px]',
  tags: 'flex items-center flex-[1_0_60px]',
  useSceneNumbering: 'flex items-center flex-[0_0_145px]',
  monitorNewItems: 'flex items-center flex-[0_0_175px]',
  actions: 'flex items-center flex-[0_1_90px] min-w-[60px]',
  collection: 'flex items-center flex-[4_0_110px]',
  originalTitle: 'flex items-center flex-[4_0_110px]',
  minimumAvailability: 'flex items-center flex-[0_0_140px]',
  studio: 'flex items-center flex-[2_0_90px]',
  inCinemas: 'flex items-center flex-[0_0_180px]',
  physicalRelease: 'flex items-center flex-[0_0_180px]',
  digitalRelease: 'flex items-center flex-[0_0_180px]',
  releaseDate: 'flex items-center flex-[0_0_180px]',
  keywords: 'flex items-center flex-[0_0_180px]',
  popularity: 'flex items-center flex-[0_0_100px]',
  runtime: 'flex items-center flex-[0_0_100px]',
  movieStatus: 'flex items-center justify-center flex-[0_0_150px] flex-col',
  movieReleaseStatus: 'flex items-center flex-[0_0_150px]',
  movieFileQualities: 'flex items-center flex-[0_0_220px]',
  imdbRating: 'flex items-center flex-[0_0_80px]',
  tmdbRating: 'flex items-center flex-[0_0_80px]',
  rottenTomatoesRating: 'flex items-center flex-[0_0_80px]',
  traktRating: 'flex items-center flex-[0_0_80px]',
};

interface LibraryIndexRowProps {
  item: MediaItem;
  sortKey: string;
  columns: Column[];
  isSelectMode: boolean;
}

function renderTitleCell(
  item: MediaItem,
  series: MediaItem['series'],
  showBanners: boolean,
  hasBannerError: boolean,
  onBannerLoadError: () => void,
  onBannerLoad: () => void
) {
  if (showBanners && series) {
    return (
      <Link
        className="relative block h-[70px] bg-[var(--seriesBackgroundColor)]!"
        to={item.link}
      >
        <SeriesBanner
          className="w-[379px] h-[70px]"
          images={series.images}
          lazy={false}
          overflow={true}
          title={item.title}
          onError={onBannerLoadError}
          onLoad={onBannerLoad}
        />

        {hasBannerError && (
          <div className="absolute top-0 left-0 flex items-center justify-center p-[5px] w-full h-full text-[var(--offWhite)] text-center text-[20px]">
            {item.title}
          </div>
        )}
      </Link>
    );
  }

  if (series) {
    return <SeriesTitleLink titleSlug={series.titleSlug} title={item.title} />;
  }

  return (
    <MovieTitleLink movieId={item.id} title={item.title} year={item.year} />
  );
}

function LibraryIndexRow(props: LibraryIndexRowProps) {
  const { item, columns, isSelectMode } = props;

  const { qualityProfile, latestSeason, isRefreshing, isSearching } =
    useLibraryIndexItem(item);

  const { showBanners, showSearchAction } = useLibraryTableOptions();

  const executeCommand = useExecuteCommand();
  const [hasBannerError, setHasBannerError] = useState(false);
  const [isEditSeriesModalOpen, setIsEditSeriesModalOpen] = useState(false);
  const [isDeleteSeriesModalOpen, setIsDeleteSeriesModalOpen] = useState(false);
  const [isEditMovieModalOpen, setIsEditMovieModalOpen] = useState(false);
  const [isDeleteMovieModalOpen, setIsDeleteMovieModalOpen] = useState(false);
  const { getIsSelected, toggleSelected } = useSelect();
  const originalCountryName = useCountryName(item.series?.originalCountry);

  const onRefreshPress = useCallback(() => {
    executeCommand(
      item.type === 'series'
        ? { name: CommandNames.RefreshSeries, seriesIds: [item.id] }
        : { name: CommandNames.RefreshMovie, movieIds: [item.id] }
    );
  }, [item.id, item.type, executeCommand]);

  const onSearchPress = useCallback(() => {
    executeCommand(
      item.type === 'series'
        ? { name: CommandNames.SeriesSearch, seriesId: item.id }
        : { name: CommandNames.MoviesSearch, movieIds: [item.id] }
    );
  }, [item.id, item.type, executeCommand]);

  const onBannerLoadError = useCallback(() => setHasBannerError(true), []);
  const onBannerLoad = useCallback(() => setHasBannerError(false), []);

  const onEditSeriesPress = useCallback(
    () => setIsEditSeriesModalOpen(true),
    []
  );
  const onEditSeriesModalClose = useCallback(
    () => setIsEditSeriesModalOpen(false),
    []
  );
  const onDeleteSeriesPress = useCallback(() => {
    setIsEditSeriesModalOpen(false);
    setIsDeleteSeriesModalOpen(true);
  }, []);
  const onDeleteSeriesModalClose = useCallback(
    () => setIsDeleteSeriesModalOpen(false),
    []
  );

  const onEditMoviePress = useCallback(() => setIsEditMovieModalOpen(true), []);
  const onEditMovieModalClose = useCallback(
    () => setIsEditMovieModalOpen(false),
    []
  );
  const onDeleteMoviePress = useCallback(() => {
    setIsEditMovieModalOpen(false);
    setIsDeleteMovieModalOpen(true);
  }, []);
  const onDeleteMovieModalClose = useCallback(
    () => setIsDeleteMovieModalOpen(false),
    []
  );

  const checkInputCallback = useCallback(() => {
    // Mock handler to satisfy `onChange` being required for `CheckInput`.
  }, []);

  const onSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps<string>) => {
      toggleSelected({ id, isSelected: value, shiftKey });
    },
    [toggleSelected]
  );

  const series = item.series;
  const movie = item.movie;
  const statistics = series?.statistics;

  const seasonCount = statistics?.seasonCount ?? 0;
  const totalEpisodeCount = statistics?.totalEpisodeCount ?? 0;
  const sizeOnDisk = item.sizeOnDisk ?? 0;
  const releaseGroups =
    movie?.statistics?.releaseGroups ?? statistics?.releaseGroups ?? [];
  const releaseTypes = statistics?.releaseTypes ?? [];
  const episodeFileQualities = statistics?.episodeFileQualities ?? [];
  const movieFileQualities = movie?.statistics?.movieFileQualities ?? [];
  const tags = series?.tags ?? movie?.tags ?? [];
  const genres = item.genres ?? [];
  const keywords = movie?.keywords ?? [];
  const qualityProfileName = qualityProfile?.name ?? '';

  return (
    <>
      {isSelectMode ? (
        <VirtualTableSelectCell
          id={item.selectKey}
          isSelected={getIsSelected(item.selectKey)}
          isDisabled={false}
          onSelectedChange={onSelectedChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'status') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <LibraryStatusCell
              key={name}
              className={COLUMN_CLASSES[name]}
              item={item}
              isSelectMode={isSelectMode}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'movieReleaseStatus') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <LibraryStatusCell
              key={name}
              className={COLUMN_CLASSES[name]}
              item={item}
              isSelectMode={isSelectMode}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'movieStatus') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <LibraryIndexProgressBar
                item={item}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'sortTitle') {
          return (
            <VirtualTableRowCell
              key={name}
              className={classNames(
                COLUMN_CLASSES[name],
                showBanners && 'flex-[0_0_379px]!',
                showBanners && !hasGrowableColumns(columns) && 'grow!'
              )}
            >
              {renderTitleCell(
                item,
                series,
                showBanners,
                hasBannerError,
                onBannerLoadError,
                onBannerLoad
              )}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalTitle') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {movie?.originalTitle}
            </VirtualTableRowCell>
          );
        }

        if (name === 'collection') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {movie?.collection?.title}
            </VirtualTableRowCell>
          );
        }

        if (name === 'seriesType') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {titleCase(item.seriesType)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'network') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {series?.network}
            </VirtualTableRowCell>
          );
        }

        if (name === 'studio') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {movie?.studio}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalCountry') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {originalCountryName}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalLanguage') {
          const language = series?.originalLanguage ?? movie?.originalLanguage;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {language?.name}
            </VirtualTableRowCell>
          );
        }

        if (name === 'qualityProfileId') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {qualityProfileName}
            </VirtualTableRowCell>
          );
        }

        if (name === 'nextAiring') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={COLUMN_CLASSES[name]}
              date={series?.nextAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'previousAiring') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={COLUMN_CLASSES[name]}
              date={series?.previousAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'added') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={COLUMN_CLASSES[name]}
              date={item.added}
              component={VirtualTableRowCell}
            />
          );
        }

        if (
          name === 'inCinemas' ||
          name === 'digitalRelease' ||
          name === 'physicalRelease' ||
          name === 'releaseDate'
        ) {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={COLUMN_CLASSES[name]}
              date={movie?.[name]}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'runtime') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {movie?.runtime}
            </VirtualTableRowCell>
          );
        }

        if (name === 'minimumAvailability') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {titleCase(movie?.minimumAvailability)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'seasonCount') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {seasonCount}
            </VirtualTableRowCell>
          );
        }

        if (name === 'seasonFolder') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <CheckInput
                name="seasonFolder"
                value={series?.seasonFolder ?? false}
                isDisabled={true}
                onChange={checkInputCallback}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'episodeProgress') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <LibraryIndexProgressBar
                item={item}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'latestSeason') {
          if (item.type !== 'series') {
            return null;
          }

          if (!latestSeason) {
            return (
              <VirtualTableRowCell
                key={name}
                className={COLUMN_CLASSES[name]}
              />
            );
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <LibraryIndexProgressBar
                item={item}
                seasonNumber={latestSeason.seasonNumber}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'episodeCount') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {totalEpisodeCount}
            </VirtualTableRowCell>
          );
        }

        if (name === 'year') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {item.year}
            </VirtualTableRowCell>
          );
        }

        if (name === 'path') {
          const path = series?.path ?? movie?.path;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {path}
            </VirtualTableRowCell>
          );
        }

        if (name === 'sizeOnDisk') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {formatBytes(sizeOnDisk)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'averageSizePerEpisode') {
          if (item.type !== 'series') {
            return null;
          }

          const averageSize =
            totalEpisodeCount > 0 ? sizeOnDisk / totalEpisodeCount : 0;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {averageSize ? formatBytes(averageSize) : null}
            </VirtualTableRowCell>
          );
        }

        if (name === 'genres') {
          const joinedGenres = genres.join(', ');

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedGenres}>{joinedGenres}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'keywords') {
          if (item.type !== 'movie') {
            return null;
          }

          const joinedKeywords = keywords.join(', ');

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedKeywords}>{joinedKeywords}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'ratings') {
          if (item.type === 'movie') {
            return (
              <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
                <HeartRating rating={item.rating ?? 0} />
              </VirtualTableRowCell>
            );
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <HeartRating
                rating={series?.ratings?.value ?? 0}
                votes={series?.ratings?.votes}
              />
            </VirtualTableRowCell>
          );
        }

        if (
          name === 'tmdbRating' ||
          name === 'imdbRating' ||
          name === 'rottenTomatoesRating' ||
          name === 'traktRating'
        ) {
          if (item.type !== 'movie') {
            return null;
          }

          const ratings = movie?.ratings ?? {};

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {name === 'tmdbRating' ? <TmdbRating ratings={ratings} /> : null}
              {name === 'imdbRating' ? <ImdbRating ratings={ratings} /> : null}
              {name === 'rottenTomatoesRating' ? (
                <RottenTomatoRating ratings={ratings} />
              ) : null}
              {name === 'traktRating' ? (
                <TraktRating ratings={ratings} />
              ) : null}
            </VirtualTableRowCell>
          );
        }

        if (name === 'popularity') {
          if (item.type !== 'movie') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {movie?.popularity}
            </VirtualTableRowCell>
          );
        }

        if (name === 'certification') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {item.certification}
            </VirtualTableRowCell>
          );
        }

        if (name === 'releaseGroups') {
          const joinedReleaseGroups = releaseGroups.join(', ');
          const truncatedReleaseGroups =
            releaseGroups.length > 3
              ? `${releaseGroups.slice(0, 3).join(', ')}...`
              : joinedReleaseGroups;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedReleaseGroups}>{truncatedReleaseGroups}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'releaseTypes') {
          if (item.type !== 'series') {
            return null;
          }

          const joinedReleaseTypes = releaseTypes
            .map(getReleaseTypeName)
            .join(', ');
          const truncatedReleaseTypes =
            releaseTypes.length > 3
              ? `${releaseTypes
                  .slice(0, 3)
                  .map(getReleaseTypeName)
                  .join(', ')}...`
              : joinedReleaseTypes;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedReleaseTypes}>{truncatedReleaseTypes}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'episodeFileQualities') {
          if (item.type !== 'series') {
            return null;
          }

          const joinedQualities = episodeFileQualities
            .map((q) => q.name)
            .join(', ');
          const truncatedQualities =
            episodeFileQualities.length > 3
              ? `${episodeFileQualities
                  .slice(0, 3)
                  .map((q) => q.name)
                  .join(', ')}...`
              : joinedQualities;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedQualities}>{truncatedQualities}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'movieFileQualities') {
          if (item.type !== 'movie') {
            return null;
          }

          const joinedQualities = movieFileQualities
            .map((q) => q.name)
            .join(', ');
          const truncatedQualities =
            movieFileQualities.length > 3
              ? `${movieFileQualities
                  .slice(0, 3)
                  .map((q) => q.name)
                  .join(', ')}...`
              : joinedQualities;

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <span title={joinedQualities}>{truncatedQualities}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'tags') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {item.type === 'series' ? (
                <SeriesTagList tags={tags} />
              ) : (
                <MovieTagList tags={tags} />
              )}
            </VirtualTableRowCell>
          );
        }

        if (name === 'useSceneNumbering') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <CheckInput
                className="mt-0!"
                name="useSceneNumbering"
                value={series?.useSceneNumbering ?? false}
                isDisabled={true}
                onChange={checkInputCallback}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'monitorNewItems') {
          if (item.type !== 'series') {
            return null;
          }

          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              {series?.monitorNewItems === 'all'
                ? translate('SeasonsMonitoredAll')
                : translate('SeasonsMonitoredNone')}
            </VirtualTableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <VirtualTableRowCell key={name} className={COLUMN_CLASSES[name]}>
              <SpinnerIconButton
                name={icons.REFRESH}
                title={translate('Refresh')}
                isSpinning={isRefreshing}
                onPress={onRefreshPress}
              />

              {showSearchAction ? (
                <SpinnerIconButton
                  name={icons.SEARCH}
                  title={translate('Search')}
                  isSpinning={isSearching}
                  onPress={onSearchPress}
                />
              ) : null}

              {item.type === 'series' ? (
                <IconButton
                  name={icons.EDIT}
                  title={translate('EditSeries')}
                  aria-label={translate('EditSeries')}
                  onPress={onEditSeriesPress}
                />
              ) : (
                <IconButton
                  name={icons.EDIT}
                  title={translate('EditMovie')}
                  aria-label={translate('EditMovie')}
                  onPress={onEditMoviePress}
                />
              )}
            </VirtualTableRowCell>
          );
        }

        return null;
      })}

      {item.type === 'series' ? (
        <>
          <EditSeriesModal
            isOpen={isEditSeriesModalOpen}
            seriesId={item.id}
            onModalClose={onEditSeriesModalClose}
            onDeleteSeriesPress={onDeleteSeriesPress}
          />

          <DeleteSeriesModal
            isOpen={isDeleteSeriesModalOpen}
            seriesId={item.id}
            onModalClose={onDeleteSeriesModalClose}
          />
        </>
      ) : (
        <>
          <EditMovieModal
            isOpen={isEditMovieModalOpen}
            movieId={item.id}
            onModalClose={onEditMovieModalClose}
            onDeleteMoviePress={onDeleteMoviePress}
          />

          <DeleteMovieModal
            isOpen={isDeleteMovieModalOpen}
            movieId={item.id}
            onModalClose={onDeleteMovieModalClose}
          />
        </>
      )}
    </>
  );
}

export default LibraryIndexRow;
