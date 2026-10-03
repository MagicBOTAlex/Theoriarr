import React, { useCallback, useState } from 'react';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import { useSelect } from 'App/Select/SelectContext';
import Alert from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const NO_CHANGE = 'noChange';

const FOOTER_CLASS = 'justify-between!';
const SELECTED_CLASS = 'font-bold';

export interface ChangeMonitoringModalContentProps {
  onSavePress(monitor: string): void;
  onModalClose(): void;
}

function ChangeMonitoringModalContent({
  onSavePress,
  onModalClose,
}: ChangeMonitoringModalContentProps) {
  const [monitor, setMonitor] = useState(NO_CHANGE);
  const { selectedCount } = useSelect();

  const onInputChange = useCallback(
    ({ value }: { value: string }) => {
      setMonitor(value);
    },
    [setMonitor]
  );

  const onSavePressWrapper = useCallback(() => {
    onSavePress(monitor);
  }, [monitor, onSavePress]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('MonitorEpisodes')}</ModalHeader>

      <ModalBody>
        <Alert kind={kinds.INFO}>
          <div>{translate('MonitorEpisodesModalInfo')}</div>
        </Alert>

        <Form>
          <FormGroup>
            <FormLabel>
              {translate('Monitoring')}

              <Popover
                anchor={
                  <Icon className="ml-[5px] mb-[-2px]" name={icons.INFO} />
                }
                title={translate('MonitoringOptions')}
                body={<SeriesMonitoringOptionsPopoverContent />}
                position={tooltipPositions.RIGHT}
              />
            </FormLabel>

            <FormInputGroup
              type={inputTypes.MONITOR_EPISODES_SELECT}
              name="monitor"
              value={monitor}
              includeNoChange={true}
              onChange={onInputChange}
            />
          </FormGroup>
        </Form>
      </ModalBody>

      <ModalFooter className={FOOTER_CLASS}>
        <div className={SELECTED_CLASS}>
          {translate('CountSelected', { count: selectedCount })}
        </div>

        <div>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <Button onPress={onSavePressWrapper}>{translate('Save')}</Button>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default ChangeMonitoringModalContent;
