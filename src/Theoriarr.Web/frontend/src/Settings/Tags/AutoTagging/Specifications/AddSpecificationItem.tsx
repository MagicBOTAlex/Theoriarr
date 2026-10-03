import classNames from 'classnames';
import React, { useCallback } from 'react';
import Card from 'Components/Card';
import Button from 'Components/Link/Button';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuContent from 'Components/Menu/MenuContent';
import { sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { AutoTaggingSpecification } from '../useAutoTaggings';
import AddSpecificationPresetMenuItem from './AddSpecificationPresetMenuItem';

interface AddSpecificationItemProps {
  implementation: string;
  implementationName: string;
  infoLink?: string;
  presets?: AutoTaggingSpecification[];
  onSpecificationSelect: ({
    implementation,
  }: {
    implementation: string;
  }) => void;
}

export default function AddSpecificationItem({
  implementation,
  implementationName,
  infoLink,
  presets,
  onSpecificationSelect,
}: AddSpecificationItemProps) {
  const handleSpecificationSelect = useCallback(() => {
    onSpecificationSelect({ implementation });
  }, [implementation, onSpecificationSelect]);

  const hasPresets = !!presets && !!presets.length;

  return (
    <Card
      className="relative w-[300px] h-[100px]"
      overlayClassName="absolute top-0 left-0 block p-[10px] w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto"
      overlayContent={true}
      aria-label={translate('AddConditionImplementation', {
        implementationName,
      })}
      onPress={handleSpecificationSelect}
    >
      <div className="text-center font-light text-[24px]">
        {implementationName}
      </div>

      <div className="mt-[20px] text-right">
        {hasPresets ? (
          <span>
            <Button size={sizes.SMALL} onPress={handleSpecificationSelect}>
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
                {presets.map((preset, index) => {
                  return (
                    <AddSpecificationPresetMenuItem
                      key={index}
                      name={preset.name}
                      implementation={implementation}
                      onPress={handleSpecificationSelect}
                    />
                  );
                })}
              </MenuContent>
            </Menu>
          </span>
        ) : null}

        {infoLink ? (
          <Button to={infoLink} size={sizes.SMALL}>
            {translate('MoreInfo')}
          </Button>
        ) : null}
      </div>
    </Card>
  );
}
