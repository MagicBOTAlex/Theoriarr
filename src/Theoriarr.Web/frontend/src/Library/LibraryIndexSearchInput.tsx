import React, { useCallback, useRef } from 'react';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const CONTAINER_CLASS = 'relative flex items-center';

const INPUT_CLASS =
  'w-[230px] rounded-[8px] border border-[var(--inputBorderColor)] bg-[var(--inputBackgroundColor)] py-[8px] pl-[33px] pr-[30px] text-[13px] text-[var(--textColor)] outline-none transition-colors focus:border-[var(--inputFocusBorderColor)] max-[768px]:w-[160px]';

const CLEAR_BUTTON_CLASS =
  'absolute right-[8px] top-1/2 flex h-[18px] w-[18px] -translate-y-1/2 items-center justify-center rounded-full text-[var(--disabledColor)] transition-colors hover:text-[var(--textColor)]';

interface LibraryIndexSearchInputProps {
  value: string;
  onChange: (value: string) => void;
}

function LibraryIndexSearchInput({
  value,
  onChange,
}: LibraryIndexSearchInputProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  const handleChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      onChange(event.target.value);
    },
    [onChange]
  );

  const handleClear = useCallback(() => {
    onChange('');
    inputRef.current?.focus();
  }, [onChange]);

  return (
    <div className={CONTAINER_CLASS}>
      <Icon
        name={icons.SEARCH}
        size={13}
        className="pointer-events-none absolute left-[11px] top-1/2 -translate-y-1/2 text-[var(--disabledColor)]"
      />

      <input
        ref={inputRef}
        className={INPUT_CLASS}
        type="search"
        placeholder={translate('Search')}
        value={value}
        onChange={handleChange}
      />

      {value ? (
        <button
          type="button"
          className={CLEAR_BUTTON_CLASS}
          aria-label={translate('Clear')}
          title={translate('Clear')}
          onClick={handleClear}
        >
          <Icon name={icons.CLOSE} size={12} aria-hidden={true} />
        </button>
      ) : null}
    </div>
  );
}

export default LibraryIndexSearchInput;
