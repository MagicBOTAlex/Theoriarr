import React, { useCallback, useEffect } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import { CheckInputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import { OrganizePreviewModel } from './useOrganizePreview';

interface OrganizePreviewRowProps {
  id: number;
  existingPath: string;
  newPath: string;
}

function OrganizePreviewRow({
  id,
  existingPath,
  newPath,
}: OrganizePreviewRowProps) {
  const { toggleSelected, useIsSelected } = useSelect<OrganizePreviewModel>();
  const isSelected = useIsSelected(id);

  const handleSelectedChange = useCallback(
    ({ value, shiftKey }: CheckInputChanged) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [id, toggleSelected]
  );

  useEffect(() => {
    toggleSelected({
      id,
      isSelected: true,
      shiftKey: false,
    });
  }, [id, toggleSelected]);

  return (
    <div className="mb-[5px] flex border-b border-[var(--borderColor)] py-[5px] last:mb-0 last:border-b-0 last:pb-0">
      <CheckInput
        containerClassName="mr-[30px]"
        name={id.toString()}
        ariaLabel={translate('SelectRow')}
        value={isSelected}
        onChange={handleSelectedChange}
      />

      <div>
        <div>
          <Icon name={icons.SUBTRACT} kind={kinds.DANGER} />

          <span className="ml-[10px]">{existingPath}</span>
        </div>

        <div>
          <Icon name={icons.ADD} kind={kinds.SUCCESS} />

          <span className="ml-[10px]">{newPath}</span>
        </div>
      </div>
    </div>
  );
}

export default OrganizePreviewRow;
