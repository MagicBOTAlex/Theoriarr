import classNames from 'classnames';
import React, { SyntheticEvent, useCallback, useMemo } from 'react';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const TOGGLE_BUTTON_CLASS = 'p-0 [font-size:inherit]';
const TOGGLE_BUTTON_DISABLED_CLASS =
  'text-[var(--disabledColor)]! cursor-not-allowed!';

interface MonitorToggleButtonProps {
  className?: string;
  monitored: boolean;
  size?: number;
  isDisabled?: boolean;
  isSaving?: boolean;
  onPress: (value: boolean, options: { shiftKey: boolean }) => unknown;
}

function MonitorToggleButton(props: MonitorToggleButtonProps) {
  const {
    className = '',
    monitored,
    isDisabled = false,
    isSaving = false,
    size,
    onPress,
    ...otherProps
  } = props;

  const iconName = monitored ? icons.MONITORED : icons.UNMONITORED;

  const title = useMemo(() => {
    if (isDisabled) {
      return translate('ToggleMonitoredSeriesUnmonitored');
    }

    if (monitored) {
      return translate('ToggleMonitoredToUnmonitored');
    }

    return translate('ToggleUnmonitoredToMonitored');
  }, [monitored, isDisabled]);

  const handlePress = useCallback(
    (event: SyntheticEvent<HTMLLinkElement, MouseEvent>) => {
      const shiftKey = event.nativeEvent.shiftKey;

      onPress(!monitored, { shiftKey });
    },
    [monitored, onPress]
  );

  return (
    <SpinnerIconButton
      className={classNames(
        TOGGLE_BUTTON_CLASS,
        className,
        isDisabled && TOGGLE_BUTTON_DISABLED_CLASS
      )}
      name={iconName}
      size={size}
      title={title}
      aria-label={title}
      isDisabled={isDisabled}
      isSpinning={isSaving}
      {...otherProps}
      onPress={handlePress}
    />
  );
}

export default MonitorToggleButton;
