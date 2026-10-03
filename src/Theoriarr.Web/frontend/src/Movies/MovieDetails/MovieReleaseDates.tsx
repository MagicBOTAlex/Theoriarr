import React from 'react';
import Icon from 'Components/Icon';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { icons } from 'Helpers/Props';
import { Movie } from 'Movies/Movie';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDate from 'Utilities/Date/formatDate';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import translate from 'Utilities/String/translate';

type MovieReleaseDatesProps = Pick<
  Movie,
  'tmdbId' | 'inCinemas' | 'digitalRelease' | 'physicalRelease'
>;

function MovieReleaseDates({
  tmdbId,
  inCinemas,
  digitalRelease,
  physicalRelease,
}: MovieReleaseDatesProps) {
  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();

  if (!inCinemas && !physicalRelease && !digitalRelease) {
    return (
      <div>
        <div className="inline-block w-[30px] pr-[10px] text-center">
          <Icon name={icons.MISSING} />
        </div>

        <InlineMarkdown
          data={translate('NoMovieReleaseDatesAvailable', {
            url: `https://www.themoviedb.org/movie/${tmdbId}`,
          })}
        />
      </div>
    );
  }

  return (
    <>
      {inCinemas ? (
        <div
          title={`${translate('InCinemas')}: ${formatDate(
            inCinemas,
            longDateFormat
          )}`}
        >
          <div className="inline-block w-[30px] pr-[10px] text-center">
            <Icon name={icons.IN_CINEMAS} />
          </div>

          {getRelativeDate({
            date: inCinemas,
            shortDateFormat,
            showRelativeDates,
            timeFormat,
            timeForToday: false,
          })}
        </div>
      ) : null}

      {digitalRelease ? (
        <div
          title={`${translate('DigitalRelease')}: ${formatDate(
            digitalRelease,
            longDateFormat
          )}`}
        >
          <div className="inline-block w-[30px] pr-[10px] text-center">
            <Icon name={icons.MOVIE_FILE} />
          </div>

          {getRelativeDate({
            date: digitalRelease,
            shortDateFormat,
            showRelativeDates,
            timeFormat,
            timeForToday: false,
          })}
        </div>
      ) : null}

      {physicalRelease ? (
        <div
          title={`${translate('PhysicalRelease')}: ${formatDate(
            physicalRelease,
            longDateFormat
          )}`}
        >
          <div className="inline-block w-[30px] pr-[10px] text-center">
            <Icon name={icons.DISC} />
          </div>

          {getRelativeDate({
            date: physicalRelease,
            shortDateFormat,
            showRelativeDates,
            timeFormat,
            timeForToday: false,
          })}
        </div>
      ) : null}
    </>
  );
}

export default MovieReleaseDates;
