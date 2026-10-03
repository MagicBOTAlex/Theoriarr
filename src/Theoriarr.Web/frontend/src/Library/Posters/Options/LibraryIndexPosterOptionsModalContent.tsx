import React, { useCallback } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import {
  setLibraryPosterOptions,
  useLibraryPosterOptions,
} from '../../libraryOptionsStore';

const posterSizeOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'small',
    get value() {
      return translate('Small');
    },
  },
  {
    key: 'medium',
    get value() {
      return translate('Medium');
    },
  },
  {
    key: 'large',
    get value() {
      return translate('Large');
    },
  },
];

interface LibraryIndexPosterOptionsModalContentProps {
  onModalClose(...args: unknown[]): unknown;
}

function LibraryIndexPosterOptionsModalContent({
  onModalClose,
}: LibraryIndexPosterOptionsModalContentProps) {
  const options = useLibraryPosterOptions();

  const onPosterOptionChange = useCallback(
    ({ name, value }: { name: string; value: unknown }) => {
      setLibraryPosterOptions({ [name]: value });
    },
    []
  );

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('PosterOptions')}</ModalHeader>

      <ModalBody>
        <Form>
          <FormGroup>
            <FormLabel>{translate('PosterSize')}</FormLabel>
            <FormInputGroup
              type={inputTypes.SELECT}
              name="size"
              value={options.size}
              values={posterSizeOptions}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('DetailedProgressBar')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="detailedProgressBar"
              value={options.detailedProgressBar}
              helpText={translate('DetailedProgressBarHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowTitle')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showTitle"
              value={options.showTitle}
              helpText={translate('ShowSeriesTitleHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowMonitored')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showMonitored"
              value={options.showMonitored}
              helpText={translate('ShowMonitoredHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowQualityProfile')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showQualityProfile"
              value={options.showQualityProfile}
              helpText={translate('ShowQualityProfileHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowCinemaRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showCinemaRelease"
              value={options.showCinemaRelease}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowDigitalRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showDigitalRelease"
              value={options.showDigitalRelease}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowPhysicalRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showPhysicalRelease"
              value={options.showPhysicalRelease}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowReleaseDate')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showReleaseDate"
              value={options.showReleaseDate}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowTags')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showTags"
              value={options.showTags}
              helpText={translate('ShowTagsHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowSearch')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showSearchAction"
              value={options.showSearchAction}
              helpText={translate('ShowSearchHelpText')}
              onChange={onPosterOptionChange}
            />
          </FormGroup>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default LibraryIndexPosterOptionsModalContent;
