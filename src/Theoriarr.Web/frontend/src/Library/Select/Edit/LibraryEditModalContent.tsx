import React, { useCallback, useState } from 'react';
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
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

const FOOTER_EXTRA_CLASS =
  'justify-between! max-[480px]:flex-col max-[480px]:gap-[10px]';

const SELECTED_CLASS = 'font-bold';

export interface LibraryEditSavePayload {
  monitored?: boolean;
  qualityProfileId?: number;
  rootFolderPath?: string;
  tags?: number[];
}

export interface LibraryEditModalContentProps {
  selectedCount: number;
  onSavePress(payload: LibraryEditSavePayload): void;
  onModalClose(): void;
}

const NO_CHANGE = 'noChange';

const monitoredOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: NO_CHANGE,
    get value() {
      return translate('NoChange');
    },
    isDisabled: true,
  },
  {
    key: 'monitored',
    get value() {
      return translate('Monitored');
    },
  },
  {
    key: 'unmonitored',
    get value() {
      return translate('Unmonitored');
    },
  },
];

function LibraryEditModalContent(props: LibraryEditModalContentProps) {
  const { selectedCount, onSavePress, onModalClose } = props;

  const [monitored, setMonitored] = useState(NO_CHANGE);
  const [qualityProfileId, setQualityProfileId] = useState<string | number>(
    NO_CHANGE
  );
  const [rootFolderPath, setRootFolderPath] = useState(NO_CHANGE);
  const [tags, setTags] = useState<number[]>([]);

  const onInputChange = useCallback(({ name, value }: InputChanged) => {
    switch (name) {
      case 'monitored':
        setMonitored(value as string);
        break;
      case 'qualityProfileId':
        setQualityProfileId(value as string);
        break;
      case 'rootFolderPath':
        setRootFolderPath(value as string);
        break;
      case 'tags':
        setTags(value as number[]);
        break;
      default:
        console.warn('LibraryEditModalContent Unknown Input');
    }
  }, []);

  const onSavePressWrapper = useCallback(() => {
    const payload: LibraryEditSavePayload = {};

    if (monitored !== NO_CHANGE) {
      payload.monitored = monitored === 'monitored';
    }

    if (qualityProfileId !== NO_CHANGE) {
      payload.qualityProfileId = qualityProfileId as number;
    }

    if (rootFolderPath !== NO_CHANGE) {
      payload.rootFolderPath = rootFolderPath;
    }

    if (tags.length) {
      payload.tags = tags;
    }

    onSavePress(payload);
  }, [monitored, qualityProfileId, rootFolderPath, tags, onSavePress]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('EditSelectedItems')}</ModalHeader>

      <ModalBody>
        <FormGroup>
          <FormLabel>{translate('Monitored')}</FormLabel>

          <FormInputGroup
            type={inputTypes.SELECT}
            name="monitored"
            value={monitored}
            values={monitoredOptions}
            onChange={onInputChange}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>{translate('QualityProfile')}</FormLabel>

          <FormInputGroup
            type={inputTypes.QUALITY_PROFILE_SELECT}
            name="qualityProfileId"
            value={qualityProfileId}
            includeNoChange={true}
            includeNoChangeDisabled={false}
            onChange={onInputChange}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>{translate('RootFolder')}</FormLabel>

          <FormInputGroup
            type={inputTypes.ROOT_FOLDER_SELECT}
            name="rootFolderPath"
            value={rootFolderPath}
            includeNoChange={true}
            includeNoChangeDisabled={false}
            selectedValueOptions={{ includeFreeSpace: false }}
            onChange={onInputChange}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>{translate('Tags')}</FormLabel>

          <FormInputGroup
            type={inputTypes.TAG}
            name="tags"
            value={tags}
            onChange={onInputChange}
          />
        </FormGroup>
      </ModalBody>

      <ModalFooter className={FOOTER_EXTRA_CLASS}>
        <div className={SELECTED_CLASS}>
          {translate('CountSelected', { count: selectedCount })}
        </div>

        <div>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <Button onPress={onSavePressWrapper}>
            {translate('ApplyChanges')}
          </Button>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default LibraryEditModalContent;
