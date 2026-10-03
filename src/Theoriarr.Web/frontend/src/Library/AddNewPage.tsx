import React, { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import AddNewSeries from 'AddSeries/AddNewSeries/AddNewSeries';
import PageContent from 'Components/Page/PageContent';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import ServiceSelect from 'Components/ServiceSelect/ServiceSelect';
import AddNewMovie from 'Movies/AddMovie/AddNewMovie';
import { ServiceId } from 'Services';
import translate from 'Utilities/String/translate';

function AddNewPage() {
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
    <PageContent
      title={
        service === 'movies'
          ? translate('AddNewMovie')
          : translate('AddNewSeries')
      }
    >
      <PageToolbar>
        <PageToolbarSection>
          <ServiceSelect value={service} onChange={handleServiceChange} />
        </PageToolbarSection>
      </PageToolbar>

      {service === 'movies' ? <AddNewMovie /> : <AddNewSeries />}
    </PageContent>
  );
}

export default AddNewPage;
