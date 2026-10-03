import React, { useCallback, useState } from 'react';
import AddSeries from 'AddSeries/AddSeries';
import { useAppDimension } from 'App/appStore';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import MetadataAttribution from 'Components/MetadataAttribution';
import { icons, kinds, sizes } from 'Helpers/Props';
import { Statistics } from 'Series/Series';
import SeriesGenres from 'Series/SeriesGenres';
import SeriesPoster from 'Series/SeriesPoster';
import useExistingSeries from 'Series/useExistingSeries';
import translate from 'Utilities/String/translate';
import AddNewSeriesModal from './AddNewSeriesModal';

interface AddNewSeriesSearchResultProps {
  series: AddSeries;
}

function AddNewSeriesSearchResult({ series }: AddNewSeriesSearchResultProps) {
  const {
    tvdbId,
    titleSlug,
    title,
    year,
    network,
    originalLanguage,
    genres = [],
    status,
    statistics = {} as Statistics,
    ratings,
    overview,
    seriesType,
    images,
    isExcluded,
  } = series;

  const isExistingSeries = useExistingSeries(tvdbId);
  const isSmallScreen = useAppDimension('isSmallScreen');
  const [isNewAddSeriesModalOpen, setIsNewAddSeriesModalOpen] = useState(false);

  const seasonCount = statistics.seasonCount;
  const handlePress = useCallback(() => {
    setIsNewAddSeriesModalOpen(true);
  }, []);

  const handleAddSeriesModalClose = useCallback(() => {
    setIsNewAddSeriesModalOpen(false);
  }, []);

  const handleTvdbLinkPress = useCallback((event: React.SyntheticEvent) => {
    event.stopPropagation();
  }, []);

  const linkProps = isExistingSeries
    ? { to: `/series/${titleSlug}` }
    : { onPress: handlePress };
  let seasons = translate('OneSeason');

  if (seasonCount > 1) {
    seasons = translate('CountSeasons', { count: seasonCount });
  }

  return (
    <div className="relative my-[20px] p-[20px] w-full text-[inherit]">
      <Link
        className="absolute top-0 left-0 block w-full h-full bg-[var(--addSeriesBackgroundColor)]! [transition:background_500ms] hover:bg-[var(--pageBackground)]! hover:shadow-[0_0_12px_var(--black)] hover:text-[inherit] hover:no-underline hover:[transition:all_200ms_ease-in]"
        aria-label={
          isExistingSeries ? title : translate('AddSeriesWithTitle', { title })
        }
        {...linkProps}
      />

      <div className="relative top-0 left-0 flex w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto">
        {isSmallScreen ? null : (
          <SeriesPoster
            className="flex-[0_0_170px] mr-[20px] h-[250px]"
            images={images}
            size={250}
            overflow={true}
            lazy={false}
            title={title}
          />
        )}

        <div className="flex flex-[0_1_100%] flex-col overflow-hidden">
          <div className="flex max-[992px]:justify-between max-[992px]:overflow-hidden">
            <div className="flex items-end flex-[0_1_auto]">
              <div className="font-light text-[36px]">
                {title}

                {!title.includes(String(year)) && year ? (
                  <span className="ml-[10px] text-[var(--disabledColor)]">
                    ({year})
                  </span>
                ) : null}
              </div>
            </div>

            <div className="flex items-center justify-between flex-[1_0_auto] h-[55px]">
              {isExistingSeries ? (
                <Icon
                  className="ml-[10px] text-[#37bc9b] pointer-events-auto"
                  name={icons.CHECK_CIRCLE}
                  size={36}
                  title={translate('AlreadyInYourLibrary')}
                />
              ) : null}

              {isExcluded ? (
                <Icon
                  className="ml-[10px] text-[var(--dangerColor)] pointer-events-auto"
                  name={icons.DANGER}
                  size={36}
                  title={translate('SeriesInImportListExclusions')}
                />
              ) : null}

              <Link
                className="mt-[-4px] ml-auto text-[var(--textColor)]"
                to={`https://www.thetvdb.com/?tab=series&id=${tvdbId}`}
                aria-label={translate('ViewSeriesOnTvdb', { title })}
                onPress={handleTvdbLinkPress}
              >
                <Icon
                  className="ml-[10px]"
                  name={icons.EXTERNAL_LINK}
                  size={28}
                  aria-hidden={true}
                />
              </Link>
            </div>
          </div>

          <div>
            <Label size={sizes.LARGE}>
              <HeartRating
                rating={ratings.value}
                votes={ratings.votes}
                iconSize={13}
              />
            </Label>

            {originalLanguage?.name ? (
              <Label size={sizes.LARGE}>
                <Icon name={icons.LANGUAGE} size={13} />

                <span className="ml-[8px]">{originalLanguage.name}</span>
              </Label>
            ) : null}

            {network ? (
              <Label size={sizes.LARGE}>
                <Icon name={icons.NETWORK} size={13} />

                <span className="ml-[8px]">{network}</span>
              </Label>
            ) : null}

            {genres.length > 0 ? (
              <Label size={sizes.LARGE}>
                <Icon name={icons.GENRE} size={13} />
                <SeriesGenres
                  className="ml-[8px] pointer-events-auto"
                  genres={genres}
                />
              </Label>
            ) : null}

            {seasonCount ? <Label size={sizes.LARGE}>{seasons}</Label> : null}

            {status === 'ended' ? (
              <Label kind={kinds.DANGER} size={sizes.LARGE}>
                {translate('Ended')}
              </Label>
            ) : null}

            {status === 'upcoming' ? (
              <Label kind={kinds.INFO} size={sizes.LARGE}>
                {translate('Upcoming')}
              </Label>
            ) : null}
          </div>

          <div className="mt-[20px] max-[992px]:mb-[20px]">{overview}</div>

          <MetadataAttribution />
        </div>
      </div>

      <AddNewSeriesModal
        isOpen={isNewAddSeriesModalOpen && !isExistingSeries}
        series={series}
        initialSeriesType={seriesType}
        onModalClose={handleAddSeriesModalClose}
      />
    </div>
  );
}

export default AddNewSeriesSearchResult;
