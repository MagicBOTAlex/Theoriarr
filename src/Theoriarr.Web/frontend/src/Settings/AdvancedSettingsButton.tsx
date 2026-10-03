import classNames from 'classnames';
import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import {
  TOOLBAR_BUTTON_CLASS,
  TOOLBAR_BUTTON_LABEL_CLASS,
  TOOLBAR_BUTTON_LABEL_CONTAINER_CLASS,
} from 'Components/Page/Toolbar/PageToolbarButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import {
  toggleShowAdvancedSettings,
  useShowAdvancedSettings,
} from './advancedSettingsStore';

const BUTTON_CLASS = `${TOOLBAR_BUTTON_CLASS} relative`;

const INDICATOR_CONTAINER_CLASS = 'absolute top-2.5 right-3';

const INDICATOR_BACKGROUND_CLASS = 'text-[var(--themeDarkColor)]';

const ENABLED_CLASS = 'text-[var(--successColor)]';

const DISABLED_CLASS = 'text-[var(--dangerColor)]';

interface AdvancedSettingsButtonProps {
  showLabel: boolean;
}

function AdvancedSettingsButton({ showLabel }: AdvancedSettingsButtonProps) {
  const showAdvancedSettings = useShowAdvancedSettings();

  const handlePress = useCallback(() => {
    toggleShowAdvancedSettings();
  }, []);

  return (
    <Link
      className={BUTTON_CLASS}
      title={
        showAdvancedSettings
          ? translate('ShownClickToHide')
          : translate('HiddenClickToShow')
      }
      onPress={handlePress}
    >
      <Icon name={icons.ADVANCED_SETTINGS} size={21} />

      <span
        className={classNames(INDICATOR_CONTAINER_CLASS, 'fa-layers fa-fw')}
      >
        <Icon
          className={INDICATOR_BACKGROUND_CLASS}
          name={icons.CIRCLE}
          size={16}
        />

        <Icon
          className={showAdvancedSettings ? ENABLED_CLASS : DISABLED_CLASS}
          name={showAdvancedSettings ? icons.CHECK : icons.CLOSE}
          size={10}
        />
      </span>

      {showLabel ? (
        <div className={TOOLBAR_BUTTON_LABEL_CONTAINER_CLASS}>
          <div className={TOOLBAR_BUTTON_LABEL_CLASS}>
            {showAdvancedSettings
              ? translate('HideAdvanced')
              : translate('ShowAdvanced')}
          </div>
        </div>
      ) : null}
    </Link>
  );
}

export default AdvancedSettingsButton;
