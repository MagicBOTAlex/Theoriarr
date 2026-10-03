import classNames from 'classnames';
import React, { useCallback } from 'react';
import Card from 'Components/Card';
import Button from 'Components/Link/Button';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuContent from 'Components/Menu/MenuContent';
import { sizes } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import AddImportListPresetMenuItem from './AddImportListPresetMenuItem';
import { ImportListModel } from './useImportLists';

const OVERLAY_CLASS =
  'absolute top-0 left-0 block w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto p-[10px]';
const NAME_CLASS = 'text-center font-light text-[24px]';
const ACTIONS_CLASS = 'mt-[20px] text-right';

interface AddImportListItemProps {
  implementation: string;
  implementationName: string;
  infoLink: string;
  presets?: ImportListModel[];
  onImportListSelect: (selectedSchema: SelectedSchema) => void;
}

function AddImportListItem({
  implementation,
  implementationName,
  infoLink,
  presets,
  onImportListSelect,
}: AddImportListItemProps) {
  const hasPresets = !!(presets && presets.length);

  const handleImportListSelect = useCallback(() => {
    onImportListSelect({ implementation, implementationName });
  }, [implementation, implementationName, onImportListSelect]);

  return (
    <Card
      className="relative w-[300px] h-[100px]"
      overlayClassName={OVERLAY_CLASS}
      overlayContent={true}
      aria-label={translate('AddImportListImplementation', {
        implementationName,
      })}
      onPress={handleImportListSelect}
    >
      <div className={NAME_CLASS}>{implementationName}</div>

      <div className={ACTIONS_CLASS}>
        {hasPresets && (
          <span>
            <Button size={sizes.SMALL} onPress={handleImportListSelect}>
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
                    <AddImportListPresetMenuItem
                      key={preset.name}
                      name={preset.name}
                      implementation={implementation}
                      implementationName={implementationName}
                      onPress={onImportListSelect}
                    />
                  );
                })}
              </MenuContent>
            </Menu>
          </span>
        )}

        <Button to={infoLink} size={sizes.SMALL}>
          {translate('MoreInfo')}
        </Button>
      </div>
    </Card>
  );
}

export default AddImportListItem;
