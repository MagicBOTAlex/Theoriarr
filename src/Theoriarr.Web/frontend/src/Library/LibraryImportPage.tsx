import React, { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import ImportSeriesPage from 'AddSeries/ImportSeries/ImportSeriesPage';
import PageContent from 'Components/Page/PageContent';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import ServiceSelect from 'Components/ServiceSelect/ServiceSelect';
import MovieLibraryImportPage from 'Movies/MovieImport/MovieLibraryImportPage';
import { ServiceId } from 'Services';
import translate from 'Utilities/String/translate';
import ImportAllButton from './ImportAllButton';

function LibraryImportPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const service: ServiceId =
    searchParams.get('type') === 'movies' ? 'movies' : 'series';

  const handleServiceChange = useCallback(
    (value: ServiceId) => {
      setSearchParams(value === 'movies' ? { type: 'movies' } : {}, {
        replace: true,
      });
    },
    [setSearchParams]
  );

  return (
    <PageContent title={translate('LibraryImport')}>
      <PageToolbar>
        <PageToolbarSection>
          <ServiceSelect value={service} onChange={handleServiceChange} />
          <ImportAllButton />
        </PageToolbarSection>
      </PageToolbar>

      {service === 'movies' ? <MovieLibraryImportPage /> : <ImportSeriesPage />}
    </PageContent>
  );
}

export default LibraryImportPage;
