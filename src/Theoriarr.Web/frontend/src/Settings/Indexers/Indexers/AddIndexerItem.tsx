import classNames from 'classnames';
import React, { useCallback } from 'react';
import Card from 'Components/Card';
import Button from 'Components/Link/Button';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuContent from 'Components/Menu/MenuContent';
import { sizes } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import { IndexerModel } from '../useIndexers';
import AddIndexerPresetMenuItem from './AddIndexerPresetMenuItem';

const OVERLAY_CLASS =
  'absolute top-0 left-0 block p-[10px] w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto';

interface AddIndexerItemProps {
  implementation: string;
  implementationName: string;
  infoLink: string;
  presets?: IndexerModel[];
  onIndexerSelect: (selectedSchema: SelectedSchema) => void;
}

function AddIndexerItem({
  implementation,
  implementationName,
  infoLink,
  presets,
  onIndexerSelect,
}: AddIndexerItemProps) {
  const hasPresets = !!presets && !!presets.length;

  const handleIndexerSelect = useCallback(() => {
    onIndexerSelect({ implementation, implementationName });
  }, [implementation, implementationName, onIndexerSelect]);

  return (
    <Card
      className="relative w-[300px] h-[100px]"
      overlayClassName={OVERLAY_CLASS}
      overlayContent={true}
      aria-label={translate('AddIndexerImplementation', { implementationName })}
      onPress={handleIndexerSelect}
    >
      <div className="text-center [font-weight:lighter] text-[24px]">
        {implementationName}
      </div>

      <div className="mt-[20px] text-right">
        {hasPresets && (
          <span>
            <Button size={sizes.SMALL} onPress={handleIndexerSelect}>
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
                    <AddIndexerPresetMenuItem
                      key={preset.name}
                      name={preset.name}
                      implementation={implementation}
                      implementationName={implementationName}
                      onPress={onIndexerSelect}
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

export default AddIndexerItem;
