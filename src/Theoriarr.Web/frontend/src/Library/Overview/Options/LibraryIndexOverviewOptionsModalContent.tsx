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
  setLibraryOverviewOptions,
  useLibraryOverviewOptions,
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

interface LibraryIndexOverviewOptionsModalContentProps {
  onModalClose(...args: unknown[]): void;
}

function LibraryIndexOverviewOptionsModalContent({
  onModalClose,
}: LibraryIndexOverviewOptionsModalContentProps) {
  const options = useLibraryOverviewOptions();

  const onOverviewOptionChange = useCallback(
    ({ name, value }: { name: string; value: unknown }) => {
      setLibraryOverviewOptions({ [name]: value });
    },
    []
  );

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('OverviewOptions')}</ModalHeader>

      <ModalBody>
        <Form>
          <FormGroup>
            <FormLabel>{translate('PosterSize')}</FormLabel>
            <FormInputGroup
              type={inputTypes.SELECT}
              name="size"
              value={options.size}
              values={posterSizeOptions}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('DetailedProgressBar')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="detailedProgressBar"
              value={options.detailedProgressBar}
              helpText={translate('DetailedProgressBarHelpText')}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowMonitored')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showMonitored"
              value={options.showMonitored}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowNetwork')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showNetwork"
              value={options.showNetwork}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowStudio')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showStudio"
              value={options.showStudio}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowCollection')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showCollection"
              value={options.showCollection}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowQualityProfile')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showQualityProfile"
              value={options.showQualityProfile}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowPreviousAiring')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showPreviousAiring"
              value={options.showPreviousAiring}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowDateAdded')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showAdded"
              value={options.showAdded}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowCinemaRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showCinemaRelease"
              value={options.showCinemaRelease}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowDigitalRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showDigitalRelease"
              value={options.showDigitalRelease}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowPhysicalRelease')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showPhysicalRelease"
              value={options.showPhysicalRelease}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowReleaseDate')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showReleaseDate"
              value={options.showReleaseDate}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowRuntime')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showRuntime"
              value={options.showRuntime}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowSeasonCount')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showSeasonCount"
              value={options.showSeasonCount}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowPath')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showPath"
              value={options.showPath}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowSizeOnDisk')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showSizeOnDisk"
              value={options.showSizeOnDisk}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowTags')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showTags"
              value={options.showTags}
              onChange={onOverviewOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ShowSearch')}</FormLabel>
            <FormInputGroup
              type={inputTypes.CHECK}
              name="showSearchAction"
              value={options.showSearchAction}
              helpText={translate('ShowSearchHelpText')}
              onChange={onOverviewOptionChange}
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

export default LibraryIndexOverviewOptionsModalContent;
