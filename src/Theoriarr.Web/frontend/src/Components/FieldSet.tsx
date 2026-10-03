import classNames from 'classnames';
import React, { ComponentProps } from 'react';
import { sizes } from 'Helpers/Props';
import { Size } from 'Helpers/Props/sizes';

const FIELD_SET_CLASS = 'm-0 mb-5 min-w-0 border-0 p-0';

const LEGEND_CLASS =
  'mb-[21px] block w-full border-0 border-b border-[#e5e5e5] p-0 text-[21px] leading-[inherit] text-[var(--textColor)]';

const LEGEND_SMALL_CLASS = 'text-[18px] text-[#909293]';

interface FieldSetProps {
  size?: Size;
  legend?: ComponentProps<'legend'>['children'];
  children?: React.ReactNode;
}

function FieldSet({ size = sizes.MEDIUM, legend, children }: FieldSetProps) {
  return (
    <fieldset className={FIELD_SET_CLASS}>
      <legend
        className={classNames(
          LEGEND_CLASS,
          size === sizes.SMALL && LEGEND_SMALL_CLASS
        )}
      >
        {legend}
      </legend>
      {children}
    </fieldset>
  );
}

export default FieldSet;
