import React, { useCallback } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import SettingsToolbar from 'Settings/SettingsToolbar';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import TheTvdb from './TheTvdb';
import Tmdb from './Tmdb';
import { useManageMetadataSource } from './useMetadataSource';

function MetadataSourceSettings() {
  const {
    isFetching,
    isFetched,
    isSaving,
    error,
    saveError,
    settings,
    hasSettings,
    hasPendingChanges,
    validationErrors,
    validationWarnings,
    saveSettings,
    updateSetting,
  } = useManageMetadataSource();

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      // @ts-expect-error - InputChanged name/value are not typed as keyof MetadataSourceSettingsModel
      updateSetting(name, value);
    },
    [updateSetting]
  );

  const warning = settings?.warning?.value;
  const resolvedAddress = settings?.resolvedProvidarrBaseUrl?.value;

  return (
    <PageContent title={translate('MetadataSourceSettings')}>
      <SettingsToolbar
        hasPendingChanges={hasPendingChanges}
        isSaving={isSaving}
        onSavePress={saveSettings}
      />

      <PageContentBody>
        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>
            {translate('MetadataSourceSettingsLoadError')}
          </Alert>
        ) : null}

        {hasSettings && isFetched && !error ? (
          <>
            <FieldSet legend={translate('MetadataProvider')}>
              {warning ? <Alert kind={kinds.WARNING}>{warning}</Alert> : null}

              {saveError ? (
                <Alert kind={kinds.DANGER}>
                  {getErrorMessage(
                    saveError,
                    translate('MetadataSourceSettingsSaveError')
                  )}
                </Alert>
              ) : null}

              <Form
                id="metadataSourceSettings"
                validationErrors={validationErrors}
                validationWarnings={validationWarnings}
              >
                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel>{translate('ProvidarrAddress')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT}
                    name="providarrBaseUrl"
                    placeholder="https://providarr.deprived.dev"
                    helpText={translate('ProvidarrAddressHelpText')}
                    onChange={handleInputChange}
                    {...settings.providarrBaseUrl}
                  />
                </FormGroup>

                {resolvedAddress ? (
                  <div className="mb-4 text-sm opacity-60">
                    {translate('ProvidarrAddressResolved')}: {resolvedAddress}
                  </div>
                ) : null}
              </Form>
            </FieldSet>

            <TheTvdb />
            <Tmdb />
          </>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default MetadataSourceSettings;
