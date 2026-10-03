import React, { ReactNode, useMemo } from 'react';
import Label from 'Components/Label';
import ArrayElement from 'typings/Helpers/ArrayElement';
import { EnhancedSelectInputValue } from './EnhancedSelectInput';
import EnhancedSelectInputSelectedValue from './EnhancedSelectInputSelectedValue';

const SELECTED_VALUE_CLASS =
  'flex flex-[1_1_auto] items-center justify-between overflow-hidden';

const VALUE_TEXT_CLASS = 'max-w-full flex-[0_0_auto] truncate';

const HINT_TEXT_CLASS =
  'ml-[15px] max-w-full flex-[1_10_0] truncate text-right text-[12px] text-[var(--gray)]';

interface HintedSelectInputSelectedValueProps<T, V> {
  selectedValue: V;
  values: T[];
  hint?: ReactNode;
  isMultiSelect?: boolean;
  includeHint?: boolean;
}

function HintedSelectInputSelectedValue<
  T extends EnhancedSelectInputValue<V>,
  V extends number | string
>(props: HintedSelectInputSelectedValueProps<T, V>) {
  const {
    selectedValue,
    values,
    hint,
    isMultiSelect = false,
    includeHint = true,
    ...otherProps
  } = props;

  const valuesMap = useMemo(() => {
    return new Map(values.map((v) => [v.key, v.value]));
  }, [values]);

  return (
    <EnhancedSelectInputSelectedValue
      className={SELECTED_VALUE_CLASS}
      {...otherProps}
    >
      <div className={VALUE_TEXT_CLASS}>
        {isMultiSelect && Array.isArray(selectedValue)
          ? selectedValue.map((key) => {
              const v = valuesMap.get(key);

              return <Label key={key}>{v ? v : key}</Label>;
            })
          : valuesMap.get(selectedValue as ArrayElement<V>)}
      </div>

      {hint != null && includeHint ? (
        <div className={HINT_TEXT_CLASS}>{hint}</div>
      ) : null}
    </EnhancedSelectInputSelectedValue>
  );
}

export default HintedSelectInputSelectedValue;
