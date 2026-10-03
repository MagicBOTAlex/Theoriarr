import React from 'react';
import { AlternateTitle } from 'Series/Series';

interface SeriesAlternateTitlesProps {
  alternateTitles: AlternateTitle[];
}

function SeriesAlternateTitles({
  alternateTitles,
}: SeriesAlternateTitlesProps) {
  return (
    <ul>
      {alternateTitles.map((alternateTitle) => {
        return (
          <li key={alternateTitle.title} className="whitespace-nowrap">
            {alternateTitle.title}
            {alternateTitle.comment ? (
              <span className="text-[12px] text-[var(--darkGray)]">
                {' '}
                {alternateTitle.comment}
              </span>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

export default SeriesAlternateTitles;
