import React, { useCallback, useEffect, useMemo, useState } from 'react';
import AddSeries from 'AddSeries/AddSeries';
import {
  AddSeriesOptions,
  setAddSeriesOption,
  useAddSeriesOptions,
} from 'AddSeries/addSeriesOptionsStore';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import SeriesTypePopoverContent from 'AddSeries/SeriesTypePopoverContent';
import { useAppDimension } from 'App/appStore';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { getValidationFailures } from 'Helpers/Hooks/useApiMutation';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import { SeriesType } from 'Series/Series';
import SeriesPoster from 'Series/SeriesPoster';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';
import { useAddSeries } from './useAddSeries';

const CONTAINER_CLASS = 'flex';

const YEAR_CLASS = 'ml-[5px] text-[var(--disabledColor)]';

const POSTER_CLASS = 'flex-[0_0_170px] mr-[20px] h-[250px]';

const INFO_CLASS = 'grow';

const OVERVIEW_CLASS = 'mb-[30px]';

const LABEL_ICON_CLASS = 'ml-[8px]';

const SEARCH_LABEL_CONTAINER_CLASS = 'flex justify-end mt-[2px]';

const SEARCH_LABEL_CLASS = 'mr-[8px] font-normal';

const MODAL_FOOTER_EXTRA_CLASS = 'max-[768px]:block max-[768px]:text-center';

const ADD_BUTTON_CLASS =
  'overflow-hidden! max-w-full text-ellipsis! whitespace-nowrap! max-[768px]:mt-[10px]';

export interface AddNewSeriesModalContentProps {
  series: AddSeries;
  initialSeriesType: SeriesType;
  onModalClose: () => void;
}

function AddNewSeriesModalContent({
  series,
  initialSeriesType,
  onModalClose,
}: AddNewSeriesModalContentProps) {
  const { title, year, overview, images, folder } = series;
  const options = useAddSeriesOptions();
  const isSmallScreen = useAppDimension('isSmallScreen');
  const isWindows = useIsWindows();

  const { isAdding, addError, addSeries } = useAddSeries();

  const { settings, validationErrors, validationWarnings } = useMemo(() => {
    return {
      ...selectSettings(options, {}),
      ...getValidationFailures(addError),
    };
  }, [options, addError]);

  const [seriesType, setSeriesType] = useState<SeriesType>(
    initialSeriesType === 'standard'
      ? settings.seriesType.value
      : initialSeriesType
  );

  const {
    monitor,
    qualityProfileId,
    rootFolderPath,
    searchForCutoffUnmetEpisodes,
    searchForMissingEpisodes,
    seasonFolder,
    seriesType: seriesTypeSetting,
    tags,
  } = settings;

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string | number | boolean | number[]>) => {
      setAddSeriesOption(name as keyof AddSeriesOptions, value);
    },
    []
  );

  const handleQualityProfileIdChange = useCallback(
    ({ value }: InputChanged<string | number>) => {
      setAddSeriesOption('qualityProfileId', value as number);
    },
    []
  );

  const handleAddSeriesPress = useCallback(() => {
    addSeries({
      ...series,
      rootFolderPath: rootFolderPath.value,
      addOptions: {
        monitor: monitor.value,
        searchForMissingEpisodes: searchForMissingEpisodes.value,
        searchForCutoffUnmetEpisodes: searchForCutoffUnmetEpisodes.value,
      },
      qualityProfileId: qualityProfileId.value,
      seriesType,
      seasonFolder: seasonFolder.value,
      tags: tags.value,
    });
  }, [
    series,
    seriesType,
    rootFolderPath,
    monitor,
    qualityProfileId,
    seasonFolder,
    searchForMissingEpisodes,
    searchForCutoffUnmetEpisodes,
    tags,
    addSeries,
  ]);

  useEffect(() => {
    setSeriesType(seriesTypeSetting.value);
  }, [seriesTypeSetting]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {title}

        {!title.includes(String(year)) && year ? (
          <span className={YEAR_CLASS}>({year})</span>
        ) : null}
      </ModalHeader>

      <ModalBody>
        <div className={CONTAINER_CLASS}>
          {isSmallScreen ? null : (
            <div className={POSTER_CLASS}>
              <SeriesPoster
                className={POSTER_CLASS}
                images={images}
                size={250}
                title={title}
              />
            </div>
          )}

          <div className={INFO_CLASS}>
            {overview ? <div className={OVERVIEW_CLASS}>{overview}</div> : null}

            <Form
              validationErrors={validationErrors}
              validationWarnings={validationWarnings}
            >
              <FormGroup>
                <FormLabel>{translate('RootFolder')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.ROOT_FOLDER_SELECT}
                  name="rootFolderPath"
                  valueOptions={{
                    seriesFolder: folder,
                    isWindows,
                  }}
                  selectedValueOptions={{
                    seriesFolder: folder,
                    isWindows,
                  }}
                  helpText={translate('AddNewSeriesRootFolderHelpText', {
                    folder,
                  })}
                  onChange={handleInputChange}
                  {...rootFolderPath}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('Monitor')}

                  <Popover
                    anchor={
                      <Icon className={LABEL_ICON_CLASS} name={icons.INFO} />
                    }
                    title={translate('MonitoringOptions')}
                    body={<SeriesMonitoringOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.MONITOR_EPISODES_SELECT}
                  name="monitor"
                  onChange={handleInputChange}
                  {...monitor}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('QualityProfile')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.QUALITY_PROFILE_SELECT}
                  name="qualityProfileId"
                  onChange={handleQualityProfileIdChange}
                  {...qualityProfileId}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SeriesType')}

                  <Popover
                    anchor={
                      <Icon className={LABEL_ICON_CLASS} name={icons.INFO} />
                    }
                    title={translate('SeriesTypes')}
                    body={<SeriesTypePopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.SERIES_TYPE_SELECT}
                  name="seriesType"
                  onChange={handleInputChange}
                  {...seriesTypeSetting}
                  value={seriesType}
                  helpText={translate('SeriesTypesHelpText')}
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

              <FormGroup>
                <FormLabel>{translate('Tags')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.TAG}
                  name="tags"
                  onChange={handleInputChange}
                  {...tags}
                />
              </FormGroup>
            </Form>
          </div>
        </div>
      </ModalBody>

      <ModalFooter className={MODAL_FOOTER_EXTRA_CLASS}>
        <div>
          <label className={SEARCH_LABEL_CONTAINER_CLASS}>
            <span className={SEARCH_LABEL_CLASS}>
              {translate('AddNewSeriesSearchForMissingEpisodes')}
            </span>

            <CheckInput
              containerClassName="relative flex flex-[1_1_65%] select-none flex-[0_1_0]!"
              className="mt-0!"
              name="searchForMissingEpisodes"
              onChange={handleInputChange}
              {...searchForMissingEpisodes}
            />
          </label>

          <label className={SEARCH_LABEL_CONTAINER_CLASS}>
            <span className={SEARCH_LABEL_CLASS}>
              {translate('AddNewSeriesSearchForCutoffUnmetEpisodes')}
            </span>

            <CheckInput
              containerClassName="relative flex flex-[1_1_65%] select-none flex-[0_1_0]!"
              className="mt-0!"
              name="searchForCutoffUnmetEpisodes"
              onChange={handleInputChange}
              {...searchForCutoffUnmetEpisodes}
            />
          </label>
        </div>

        <SpinnerButton
          className={ADD_BUTTON_CLASS}
          kind={kinds.SUCCESS}
          isSpinning={isAdding}
          onPress={handleAddSeriesPress}
        >
          {translate('AddSeriesWithTitle', { title })}
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default AddNewSeriesModalContent;
