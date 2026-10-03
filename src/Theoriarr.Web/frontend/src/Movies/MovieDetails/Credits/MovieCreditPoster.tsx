import React, { useCallback, useState } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import Popover from 'Components/Tooltip/Popover';
import { icons, kinds, sizes } from 'Helpers/Props';
import { MovieImage } from 'Movies/Movie';
import MovieHeadshot from 'Movies/MovieHeadshot';
import translate from 'Utilities/String/translate';

const TITLE_CLASS =
  'overflow-hidden px-[5px] bg-[var(--movieBackgroundColor)] text-center text-ellipsis whitespace-nowrap text-[12px]';

interface MovieCreditPosterProps {
  tmdbId: number;
  personName: string;
  subtitle: string;
  images?: MovieImage[];
  posterWidth: number;
  posterHeight: number;
}

function MovieCreditPoster({
  tmdbId,
  personName,
  subtitle,
  images = [],
  posterWidth,
  posterHeight,
}: MovieCreditPosterProps) {
  const [hasPosterError, setHasPosterError] = useState(false);

  const handlePosterLoadError = useCallback(() => {
    setHasPosterError(true);
  }, []);

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
    borderRadius: '5px',
  };

  const contentStyle = {
    width: `${posterWidth}px`,
  };

  return (
    <div
      className="transition-all duration-200 ease-in hover:z-[2] hover:shadow-[0_0_12px_var(--black)]"
      style={contentStyle}
    >
      <div className="relative">
        <div className="mx-[2px]">
          <Popover
            anchor={<Icon name={icons.EXTERNAL_LINK} size={12} />}
            title={translate('Links')}
            body={
              <Link to={`https://www.themoviedb.org/person/${tmdbId}`}>
                <Label
                  className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default cursor-pointer"
                  kind={kinds.INFO}
                  size={sizes.LARGE}
                >
                  {translate('TMDb')}
                </Label>
              </Link>
            }
          />
        </div>

        <div style={elementStyle}>
          <MovieHeadshot
            className="relative block bg-[var(--defaultColor)]"
            style={elementStyle}
            images={images}
            size={250}
            lazy={false}
            overflow={true}
            onError={handlePosterLoadError}
          />

          {hasPosterError ? (
            <div className="absolute top-0 left-0 flex items-center justify-center p-[5px] w-full h-full text-[var(--offWhite)] text-center text-[20px]">
              {personName}
            </div>
          ) : null}
        </div>
      </div>

      <div className={TITLE_CLASS}>{personName}</div>
      <div className={TITLE_CLASS}>{subtitle}</div>
    </div>
  );
}

export default MovieCreditPoster;
