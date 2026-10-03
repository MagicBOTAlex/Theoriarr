import React, { useCallback, useEffect, useMemo } from 'react';
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
import { usePendingChangesStore } from 'Helpers/Hooks/usePendingChangesStore';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { Movie } from 'Movies/Movie';
import { useSaveMovie, useSingleMovie } from 'Movies/useMovies';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';

const MINIMUM_AVAILABILITY_OPTIONS = [
  {
    key: 'announced',
    value: translate('Announced'),
  },
  {
    key: 'inCinemas',
    value: translate('InCinemas'),
  },
  {
    key: 'released',
    value: translate('Released'),
  },
];

export interface EditMovieModalContentProps {
  movieId: number;
  onModalClose: () => void;
  onDeleteMoviePress: () => void;
}

function EditMovieModalContent({
  movieId,
  onModalClose,
  onDeleteMoviePress,
}: EditMovieModalContentProps) {
  const movie = useSingleMovie(movieId)!;

  const {
    title,
    monitored,
    minimumAvailability,
    qualityProfileId,
    path,
    tags,
  } = movie;

  const { pendingChanges, setPendingChange } = usePendingChangesStore<
    Partial<Movie>
  >({});

  const isPathChanging = !!(
    pendingChanges.path && path !== pendingChanges.path
  );

  const { saveMovie, isSaving, saveError } = useSaveMovie(isPathChanging);
  const wasSaving = usePrevious(isSaving);

  const { settings, ...otherSettings } = useMemo(() => {
    return selectSettings(
      {
        monitored,
        minimumAvailability,
        qualityProfileId: qualityProfileId ?? 0,
        path,
        tags,
      },
      pendingChanges,
      saveError
    );
  }, [
    monitored,
    minimumAvailability,
    qualityProfileId,
    path,
    tags,
    pendingChanges,
    saveError,
  ]);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      setPendingChange(name as keyof Movie, value as Movie[keyof Movie]);
    },
    [setPendingChange]
  );

  const handleSavePress = useCallback(() => {
    saveMovie({
      ...movie,
      ...pendingChanges,
    });
  }, [movie, pendingChanges, saveMovie]);

  useEffect(() => {
    if (!isSaving && wasSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('EditMovieModalHeader', { title })}</ModalHeader>

      <ModalBody>
        <Form {...otherSettings}>
          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('Monitored')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="monitored"
              helpText={translate('MonitoredMovieHelpText')}
              {...settings.monitored}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('MinimumAvailability')}</FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="minimumAvailability"
              values={MINIMUM_AVAILABILITY_OPTIONS}
              {...settings.minimumAvailability}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('QualityProfile')}</FormLabel>

            <FormInputGroup
              type={inputTypes.QUALITY_PROFILE_SELECT}
              name="qualityProfileId"
              {...settings.qualityProfileId}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('Path')}</FormLabel>

            <FormInputGroup
              type={inputTypes.PATH}
              name="path"
              {...settings.path}
              includeFiles={false}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('Tags')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TAG}
              name="tags"
              {...settings.tags}
              onChange={handleInputChange}
            />
          </FormGroup>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button
          className="mr-auto"
          kind={kinds.DANGER}
          onPress={onDeleteMoviePress}
        >
          {translate('Delete')}
        </Button>

        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          error={saveError}
          isSpinning={isSaving}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default EditMovieModalContent;
