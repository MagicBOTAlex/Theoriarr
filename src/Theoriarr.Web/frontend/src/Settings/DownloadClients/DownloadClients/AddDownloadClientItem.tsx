import classNames from 'classnames';
import React, { useCallback } from 'react';
import Card from 'Components/Card';
import Button from 'Components/Link/Button';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuContent from 'Components/Menu/MenuContent';
import { sizes } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import AddDownloadClientPresetMenuItem from './AddDownloadClientPresetMenuItem';
import { DownloadClientModel } from './useDownloadClients';

const OVERLAY_CLASS =
  'absolute top-0 left-0 block p-[10px] w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto';
const NAME_CLASS = 'text-center [font-weight:lighter] text-[24px]';
const ACTIONS_CLASS = 'mt-[20px] text-right';

interface AddDownloadClientItemProps {
  implementation: string;
  implementationName: string;
  infoLink: string;
  presets?: DownloadClientModel[];
  onDownloadClientSelect: (selectedSchema: SelectedSchema) => void;
}

function AddDownloadClientItem({
  implementation,
  implementationName,
  infoLink,
  presets,
  onDownloadClientSelect,
}: AddDownloadClientItemProps) {
  const hasPresets = !!presets && !!presets.length;

  const handleDownloadClientSelect = useCallback(() => {
    onDownloadClientSelect({ implementation, implementationName });
  }, [implementation, implementationName, onDownloadClientSelect]);

  return (
    <Card
      className="relative w-[300px] h-[100px]"
      overlayClassName={OVERLAY_CLASS}
      overlayContent={true}
      aria-label={translate('AddDownloadClientImplementation', {
        implementationName,
      })}
      onPress={handleDownloadClientSelect}
    >
      <div className={NAME_CLASS}>{implementationName}</div>

      <div className={ACTIONS_CLASS}>
        {hasPresets ? (
          <span>
            <Button size={sizes.SMALL} onPress={handleDownloadClientSelect}>
              {translate('Custom')}
            </Button>

            <Menu className={classNames(MENU_CLASS, 'inline-block mx-[5px]')}>
              <Button
                className="after:ml-[5px] after:content-['▾']"
                size={sizes.SMALL}
              >
                {translate('Presets')}
              </Button>

              <MenuContent>
                {presets.map((preset) => {
                  return (
                    <AddDownloadClientPresetMenuItem
                      key={preset.name}
                      name={preset.name}
                      implementation={implementation}
                      implementationName={implementationName}
                      onPress={onDownloadClientSelect}
                    />
                  );
                })}
              </MenuContent>
            </Menu>
          </span>
        ) : null}

        <Button to={infoLink} size={sizes.SMALL}>
          {translate('MoreInfo')}
        </Button>
      </div>
    </Card>
  );
}

export default AddDownloadClientItem;
