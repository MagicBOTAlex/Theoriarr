import React, { useCallback } from 'react';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import TextInput from './TextInput';

const ITEM_CONTAINER_CLASS =
  'mb-[3px] flex border-b border-[var(--inputBorderColor)] last:mb-0 last:border-b-0';

const INPUT_CLASS =
  'w-full border-none bg-transparent text-[var(--textColor)] placeholder:text-[var(--helpTextColor)]';

interface KeyValueListInputItemProps {
  index: number;
  keyValue: string;
  value: string;
  keyPlaceholder?: string;
  valuePlaceholder?: string;
  isNew: boolean;
  onChange: (index: number, itemValue: { key: string; value: string }) => void;
  onRemove: (index: number) => void;
  onFocus: () => void;
  onBlur: () => void;
}

function KeyValueListInputItem({
  index,
  keyValue,
  value,
  keyPlaceholder = 'Key',
  valuePlaceholder = 'Value',
  isNew,
  onChange,
  onRemove,
  onFocus,
  onBlur,
}: KeyValueListInputItemProps): JSX.Element {
  const handleKeyChange = useCallback(
    ({ value: keyValue }: { value: string }) => {
      onChange(index, { key: keyValue, value });
    },
    [index, value, onChange]
  );

  const handleValueChange = useCallback(
    ({ value }: { value: string }) => {
      onChange(index, { key: keyValue, value });
    },
    [index, keyValue, onChange]
  );

  const handleRemovePress = useCallback(() => {
    onRemove(index);
  }, [index, onRemove]);

  return (
    <div className={ITEM_CONTAINER_CLASS}>
      <div className="flex-[1_0_0]">
        <TextInput
          className={INPUT_CLASS}
          name="key"
          value={keyValue}
          placeholder={keyPlaceholder}
          onChange={handleKeyChange}
          onFocus={onFocus}
          onBlur={onBlur}
        />
      </div>

      <div className="min-w-[40px] flex-[1_0_0]">
        <TextInput
          className={INPUT_CLASS}
          name="value"
          value={value}
          placeholder={valuePlaceholder}
          onChange={handleValueChange}
          onFocus={onFocus}
          onBlur={onBlur}
        />
      </div>

      <div className="flex-[0_0_22px]">
        {isNew ? null : (
          <IconButton
            name={icons.REMOVE}
            aria-label={translate('Remove')}
            tabIndex={-1}
            onPress={handleRemovePress}
          />
        )}
      </div>
    </div>
  );
}

export default KeyValueListInputItem;
