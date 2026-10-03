import React, { useCallback, useRef } from 'react';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import MovieCredit from 'typings/MovieCredit';
import MovieCreditPoster from './MovieCreditPoster';

const posterWidth = 162;
const posterHeight = 238;

interface MovieCreditPostersProps {
  items: MovieCredit[];
  subtitleKey: 'character' | 'job';
}

function MovieCreditPosters({ items, subtitleKey }: MovieCreditPostersProps) {
  const scrollerRef = useRef<HTMLDivElement>(null);

  const handleScrollLeft = useCallback(() => {
    scrollerRef.current?.scrollBy({ left: -(posterWidth + 10) * 3 });
  }, []);

  const handleScrollRight = useCallback(() => {
    scrollerRef.current?.scrollBy({ left: (posterWidth + 10) * 3 });
  }, []);

  return (
    <div className="flex items-center">
      <IconButton
        className="shrink-0 basis-auto cursor-pointer border-0 bg-transparent p-[5px] text-[var(--white)] hover:text-[var(--iconButtonHoverLightColor)]"
        name={icons.ARROW_LEFT}
        size={24}
        onPress={handleScrollLeft}
      />

      <div
        ref={scrollerRef}
        className="flex scroll-smooth gap-[10px] overflow-x-auto py-[10px]"
      >
        {items.map((credit) => {
          return (
            <div key={credit.id} className="shrink-0 basis-auto">
              <MovieCreditPoster
                tmdbId={credit.personTmdbId}
                personName={credit.personName}
                subtitle={credit[subtitleKey]}
                images={credit.images}
                posterWidth={posterWidth}
                posterHeight={posterHeight}
              />
            </div>
          );
        })}
      </div>

      <IconButton
        className="shrink-0 basis-auto cursor-pointer border-0 bg-transparent p-[5px] text-[var(--white)] hover:text-[var(--iconButtonHoverLightColor)]"
        name={icons.ARROW_RIGHT}
        size={24}
        onPress={handleScrollRight}
      />
    </div>
  );
}

export default MovieCreditPosters;
