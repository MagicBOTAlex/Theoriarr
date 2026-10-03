import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import MenuItem from 'Components/Menu/MenuItem';
import MenuItemSeparator from 'Components/Menu/MenuItemSeparator';
import { align, icons, kinds } from 'Helpers/Props';
import { useSystemStatusData } from 'System/Status/useSystemStatus';
import { useRestart, useShutdown } from 'System/useSystem';
import translate from 'Utilities/String/translate';

const MENU_BUTTON_CLASS =
  'mr-[15px] h-15 w-[30px] text-center hover:text-[var(--themeDarkColor)] max-md:mr-[5px]';

interface PageHeaderActionsMenuProps {
  onKeyboardShortcutsPress(): void;
}

function PageHeaderActionsMenu(props: PageHeaderActionsMenuProps) {
  const { onKeyboardShortcutsPress } = props;

  const { authentication, isContainerized } = useSystemStatusData();
  const { mutate: restart } = useRestart();
  const { mutate: shutdown } = useShutdown();

  const showSignOut = authentication === 'forms' || authentication === 'oidc';

  const handleRestartPress = useCallback(() => {
    restart();
  }, [restart]);

  const handleShutdownPress = useCallback(() => {
    shutdown();
  }, [shutdown]);

  return (
    <div>
      <Menu alignMenu={align.RIGHT}>
        <MenuButton className={MENU_BUTTON_CLASS} aria-label="Menu Button">
          <Icon name={icons.INTERACTIVE} title={translate('Menu')} />
        </MenuButton>

        <MenuContent>
          <MenuItem onPress={onKeyboardShortcutsPress}>
            <Icon className="mr-2" name={icons.KEYBOARD} />
            {translate('KeyboardShortcuts')}
          </MenuItem>

          <MenuItemSeparator />

          <MenuItem onPress={handleRestartPress}>
            <Icon className="mr-2" name={icons.RESTART} />
            {translate('Restart')}
          </MenuItem>

          {isContainerized ? null : (
            <MenuItem onPress={handleShutdownPress}>
              <Icon
                className="mr-2"
                name={icons.SHUTDOWN}
                kind={kinds.DANGER}
              />
              {translate('Shutdown')}
            </MenuItem>
          )}

          {showSignOut ? (
            <>
              <MenuItemSeparator />

              <MenuItem
                to={`${window.Theoriarr.services.series.apiBase}/logout`}
                noRouter={true}
              >
                <Icon className="mr-2" name={icons.LOGOUT} />
                {translate('Logout')}
              </MenuItem>
            </>
          ) : null}
        </MenuContent>
      </Menu>
    </div>
  );
}

export default PageHeaderActionsMenu;
