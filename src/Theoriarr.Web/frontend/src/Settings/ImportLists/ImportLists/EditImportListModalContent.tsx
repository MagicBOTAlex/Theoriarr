import classNames from 'classnames';
import React, { useCallback, useEffect } from 'react';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import SeriesMonitorNewItemsOptionsPopoverContent from 'AddSeries/SeriesMonitorNewItemsOptionsPopoverContent';
import SeriesTypePopoverContent from 'AddSeries/SeriesTypePopoverContent';
import Alert, { ALERT_CLASS } from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import ProviderFieldFormGroup from 'Components/Form/ProviderFieldFormGroup';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import AdvancedSettingsButton from 'Settings/AdvancedSettingsButton';
import { useShowAdvancedSettings } from 'Settings/advancedSettingsStore';
import { useSettingsService } from 'Settings/SettingsServiceContext';
import { SelectedSchema } from 'Settings/useProviderSchema';
import { EnhancedSelectInputChanged, InputChanged } from 'typings/inputs';
import formatShortTimeSpan from 'Utilities/Date/formatShortTimeSpan';
import translate from 'Utilities/String/translate';
import { useManageImportList } from './useImportLists';

const MESSAGE_CLASS = classNames(ALERT_CLASS, 'mb-[30px]');

const MOVIE_MONITOR_OPTIONS = [
  { key: 'movieOnly', value: translate('MovieOnly') },
  { key: 'movieAndCollection', value: translate('MovieAndCollection') },
  { key: 'none', value: translate('None') },
];

const MINIMUM_AVAILABILITY_OPTIONS = [
  { key: 'announced', value: translate('Announced') },
  { key: 'inCinemas', value: translate('InCinemas') },
  { key: 'released', value: translate('Released') },
];

export interface EditImportListModalContentProps {
  id?: number;
  cloneId?: number;
  selectedSchema?: SelectedSchema;
  onModalClose: () => void;
  onDeleteImportListPress?: () => void;
}

function EditImportListModalContent({
  id,
  cloneId,
  selectedSchema,
  onModalClose,
  onDeleteImportListPress,
}: EditImportListModalContentProps) {
  const showAdvancedSettings = useShowAdvancedSettings();
  const service = useSettingsService();
  const isMovies = service === 'movies';

  const {
    item,
    updateFieldValue,
    updateValue,
    saveProvider,
    isSaving,
    saveError,
    testProvider,
    isTesting,
    validationErrors,
    validationWarnings,
  } = useManageImportList(id, cloneId, selectedSchema);

  const wasSaving = usePrevious(isSaving);

  const {
    implementationName = '',
    name,
    enableAutomaticAdd,
    searchForMissingEpisodes,
    minRefreshInterval,
    shouldMonitor,
    rootFolderPath,
    monitorNewItems,
    qualityProfileId,
    seriesType,
    seasonFolder,
    tags,
    tagExisting,
    monitor,
    minimumAvailability,
    searchOnAdd,
    enableAuto,
    fields,
  } = item;

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      // @ts-expect-error - InputChanged is not typed correctly
      updateValue(change.name, change.value);
    },
    [updateValue]
  );

  const handleFieldChange = useCallback(
    ({
      name,
      value,
      additionalProperties,
    }: EnhancedSelectInputChanged<unknown>) => {
      updateFieldValue({ [name]: value, ...additionalProperties });
    },
    [updateFieldValue]
  );

  const handleTestPress = useCallback(() => {
    testProvider();
  }, [testProvider]);

  const handleSavePress = useCallback(() => {
    saveProvider();
  }, [saveProvider]);

  useEffect(() => {
    if (wasSaving && !isSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id
          ? translate('EditImportListImplementation', { implementationName })
          : translate('AddImportListImplementation', { implementationName })}
      </ModalHeader>

      <ModalBody>
        <Form
          validationErrors={validationErrors}
          validationWarnings={validationWarnings}
        >
          <Alert kind={kinds.INFO} className={MESSAGE_CLASS}>
            {translate('ListWillRefreshEveryInterval', {
              refreshInterval: formatShortTimeSpan(minRefreshInterval.value),
            })}
          </Alert>

          <FormGroup>
            <FormLabel>{translate('Name')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="name"
              {...name}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('EnableAutomaticAdd')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="enableAutomaticAdd"
              helpText={translate('EnableAutomaticAddSeriesHelpText')}
              {...enableAutomaticAdd}
              onChange={handleInputChange}
            />
          </FormGroup>

          {isMovies ? null : (
            <>
              <FormGroup>
                <FormLabel>
                  {translate('ImportListSearchForMissingEpisodes')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="searchForMissingEpisodes"
                  helpText={translate(
                    'ImportListSearchForMissingEpisodesHelpText'
                  )}
                  {...searchForMissingEpisodes}
                  onChange={handleInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('Monitor')}

                  <Popover
                    anchor={<Icon className="ml-[8px]" name={icons.INFO} />}
                    title={translate('MonitoringOptions')}
                    body={<SeriesMonitoringOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.MONITOR_EPISODES_SELECT}
                  name="shouldMonitor"
                  onChange={handleInputChange}
                  {...shouldMonitor}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MonitorNewSeasons')}
                  <Popover
                    anchor={<Icon className="ml-[8px]" name={icons.INFO} />}
                    title={translate('MonitorNewSeasons')}
                    body={<SeriesMonitorNewItemsOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.MONITOR_NEW_ITEMS_SELECT}
                  name="monitorNewItems"
                  helpText={translate('MonitorNewSeasonsHelpText')}
                  {...monitorNewItems}
                  onChange={handleInputChange}
                />
              </FormGroup>
            </>
          )}

          {isMovies ? (
            <>
              <FormGroup>
                <FormLabel>
                  {translate('Monitor')}

                  <Popover
                    anchor={<Icon className="ml-[8px]" name={icons.INFO} />}
                    title={translate('MonitoringOptions')}
                    body={translate('MovieMonitoringOptionsHelpText')}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="monitor"
                  values={MOVIE_MONITOR_OPTIONS}
                  onChange={handleInputChange}
                  {...monitor}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('MinimumAvailability')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.SELECT}
                  name="minimumAvailability"
                  values={MINIMUM_AVAILABILITY_OPTIONS}
                  onChange={handleInputChange}
                  {...minimumAvailability}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('SearchOnAdd')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="searchOnAdd"
                  helpText={translate('SearchOnAddHelpText')}
                  onChange={handleInputChange}
                  {...searchOnAdd}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('EnableAuto')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableAuto"
                  helpText={translate('EnableAutoHelpText')}
                  onChange={handleInputChange}
                  {...enableAuto}
                />
              </FormGroup>
            </>
          ) : null}

          <FormGroup>
            <FormLabel>{translate('RootFolder')}</FormLabel>

            <FormInputGroup
              type={inputTypes.ROOT_FOLDER_SELECT}
              name="rootFolderPath"
              helpText={translate('ListRootFolderHelpText')}
              {...rootFolderPath}
              includeMissingValue={true}
              onChange={handleInputChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('QualityProfile')}</FormLabel>

            <FormInputGroup
              type={inputTypes.QUALITY_PROFILE_SELECT}
              name="qualityProfileId"
              helpText={translate('ListQualityProfileHelpText')}
              {...qualityProfileId}
              onChange={handleInputChange}
            />
          </FormGroup>

          {isMovies ? null : (
            <>
              <FormGroup>
                <FormLabel>
                  {translate('SeriesType')}

                  <Popover
                    anchor={<Icon className="ml-[8px]" name={icons.INFO} />}
                    title={translate('SeriesTypes')}
                    body={<SeriesTypePopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.SERIES_TYPE_SELECT}
                  name="seriesType"
                  onChange={handleInputChange}
                  {...seriesType}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('SeasonFolder')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="seasonFolder"
                  onChange={handleInputChange}
                  {...seasonFolder}
                />
              </FormGroup>
            </>
          )}

          <FormGroup>
            <FormLabel>{translate('SonarrTags')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TAG}
              name="tags"
              helpText={translate('ListTagsHelpText')}
              {...tags}
              onChange={handleInputChange}
            />
          </FormGroup>

          {isMovies ? null : (
            <FormGroup>
              <FormLabel>{translate('TagExisting')}</FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="tagExisting"
                helpText={translate('TagExistingHelpText')}
                {...tagExisting}
                onChange={handleInputChange}
              />
            </FormGroup>
          )}

          {fields?.map((field) => {
            return (
              <ProviderFieldFormGroup
                key={field.name}
                advancedSettings={showAdvancedSettings}
                provider="importList"
                providerData={item}
                {...field}
                onChange={handleFieldChange}
              />
            );
          })}
        </Form>
      </ModalBody>

      <ModalFooter>
        {id ? (
          <Button
            className="mr-auto"
            kind={kinds.DANGER}
            onPress={onDeleteImportListPress}
          >
            {translate('Delete')}
          </Button>
        ) : null}

        <AdvancedSettingsButton showLabel={false} />

        <SpinnerErrorButton
          isSpinning={isTesting}
          error={saveError}
          onPress={handleTestPress}
        >
          {translate('Test')}
        </SpinnerErrorButton>

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

export default EditImportListModalContent;
