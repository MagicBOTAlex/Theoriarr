import React from 'react';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import translate from 'Utilities/String/translate';

function Tmdb() {
  return (
    <div className="mt-8 flex">
      <img
        className="h-[123px] w-[213px]"
        alt={translate('Tmdb')}
        src={`${window.Theoriarr.services.series.urlBase}/Content/Images/tmdb.svg`}
      />

      <div className="ml-[30px]">
        <div className="mb-5 text-[36px] font-light">{translate('Tmdb')}</div>

        <InlineMarkdown
          data={translate(
            'MovieAndCollectionInformationIsProvidedByTheMovieDb',
            {
              url: 'https://www.themoviedb.org/',
            }
          )}
        />

        <div className="mt-3 text-sm">{translate('TmdbAttribution')}</div>
      </div>
    </div>
  );
}

export default Tmdb;
