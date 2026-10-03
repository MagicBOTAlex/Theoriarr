import React from 'react';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import useTheme from 'Helpers/Hooks/useTheme';
import translate from 'Utilities/String/translate';

function TheTvdb() {
  const theme = useTheme();

  return (
    <div className="flex">
      <img
        className="h-[123px] w-[213px]"
        alt={translate('TheTvdb')}
        src={`${window.Theoriarr.services.series.urlBase}/Content/Images/thetvdb-${theme}.png`}
      />

      <div className="ml-[30px]">
        <div className="mb-5 text-[36px] font-light">
          {translate('TheTvdb')}
        </div>
        <InlineMarkdown
          data={translate('SeriesAndEpisodeInformationIsProvidedByTheTVDB', {
            url: 'https://www.thetvdb.com/subscribe',
          })}
        />
      </div>
    </div>
  );
}

export default TheTvdb;
