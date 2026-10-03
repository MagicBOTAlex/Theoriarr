import React, { useCallback, useEffect } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { inputTypes, kinds } from 'Helpers/Props';
import { useSettingsService } from 'Settings/SettingsServiceContext';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import { useManageImportListExclusion } from './useImportListExclusions';

interface EditImportListExclusionModalContentProps {
  id?: number;
  title?: string;
  tvdbId?: number;
  movieTitle?: string;
  tmdbId?: number;
  movieYear?: number;
  onModalClose: () => void;
  onDeleteImportListExclusionPress?: () => void;
}

function EditImportListExclusionModalContent({
  id,
  title: existingTitle,
  tvdbId: existingTvdbId,
  movieTitle: existingMovieTitle,
  tmdbId: existingTmdbId,
  movieYear: existingMovieYear,
  onModalClose,
  onDeleteImportListExclusionPress,
}: EditImportListExclusionModalContentProps) {
  const service = useSettingsService();
  const isMovies = service === 'movies';

  const {
    settings,
    isSaving,
    saveError,
    validationErrors,
    validationWarnings,
    updateValue,
    save,
  } = useManageImportListExclusion({
    id,
    title: existingTitle,
    tvdbId: existingTvdbId,
    movieTitle: existingMovieTitle,
    tmdbId: existingTmdbId,
    movieYear: existingMovieYear,
  });

  const wasSaving = usePrevious(isSaving);

  useEffect(() => {
    if (wasSaving && !isSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      updateValue(name, value);
    },
    [updateValue]
  );

  const handleSavePress = useCallback(() => {
    save();
  }, [save]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id
          ? translate('EditImportListExclusion')
          : translate('AddImportListExclusion')}
      </ModalHeader>

      <ModalBody>
        <Form
          validationErrors={validationErrors}
          validationWarnings={validationWarnings}
        >
          {isMovies ? (
            <>
              <FormGroup>
                <FormLabel>{translate('MovieTitle')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="movieTitle"
                  helpText={translate('MovieTitleToExcludeHelpText')}
                  {...settings.movieTitle}
                  onChange={handleInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('TmdbId')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="tmdbId"
                  helpText={translate('TmdbIdExcludeHelpText')}
                  {...settings.tmdbId}
                  onChange={handleInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('Year')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="movieYear"
                  helpText={translate('MovieYearToExcludeHelpText')}
                  {...settings.movieYear}
                  onChange={handleInputChange}
                />
              </FormGroup>
            </>
          ) : (
            <>
              <FormGroup>
                <FormLabel>{translate('Title')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="title"
                  helpText={translate('SeriesTitleToExcludeHelpText')}
                  {...settings.title}
                  onChange={handleInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('TvdbId')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="tvdbId"
                  helpText={translate('TvdbIdExcludeHelpText')}
                  {...settings.tvdbId}
                  onChange={handleInputChange}
                />
              </FormGroup>
            </>
          )}
        </Form>
      </ModalBody>

      <ModalFooter>
        {id ? (
          <Button
            className="mr-auto"
            kind={kinds.DANGER}
            onPress={onDeleteImportListExclusionPress}
          >
            {translate('Delete')}
          </Button>
        ) : null}

        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          isSpinning={isSaving}
          error={saveError}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default EditImportListExclusionModalContent;
