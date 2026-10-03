import React from 'react';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import EnhancedSelectInputSelectedValue from './EnhancedSelectInputSelectedValue';
import { RootFolderSelectInputValue } from './RootFolderSelectInput';

const SELECTED_VALUE_CLASS =
  'flex flex-[1_1_auto] items-center justify-between overflow-hidden';

const PATH_CONTAINER_CLASS = 'flex max-w-full flex-[1_0_0] truncate';

const SERIES_FOLDER_CLASS =
  'max-w-full flex-[0_1_auto] truncate text-[var(--disabledColor)]';

const FREE_SPACE_CLASS =
  'ml-[15px] flex-[0_0_auto] text-right text-[12px] text-[var(--gray)]';

const IS_MISSING_CLASS =
  'ml-[15px] flex-[0_0_auto] text-right text-[12px] text-[var(--dangerColor)]';

interface RootFolderSelectInputSelectedValueProps {
  selectedValue: string;
  values: RootFolderSelectInputValue[];
  seriesFolder?: string;
  isWindows?: boolean;
  includeFreeSpace?: boolean;
}

function RootFolderSelectInputSelectedValue({
  selectedValue,
  values,
  seriesFolder,
  includeFreeSpace = true,
  isWindows,
  ...otherProps
}: RootFolderSelectInputSelectedValueProps) {
  const slashCharacter = isWindows ? '\\' : '/';
  const { value, freeSpace, isMissing } =
    values.find((v) => v.key === selectedValue) ||
    ({} as RootFolderSelectInputValue);

  return (
    <EnhancedSelectInputSelectedValue
      className={SELECTED_VALUE_CLASS}
      {...otherProps}
    >
      <div className={PATH_CONTAINER_CLASS}>
        <div className="flex-[0_1_auto]">{value}</div>

        {seriesFolder ? (
          <div className={SERIES_FOLDER_CLASS}>
            {slashCharacter}
            {seriesFolder}
          </div>
        ) : null}
      </div>

      {freeSpace != null && includeFreeSpace ? (
        <div className={FREE_SPACE_CLASS}>
          {translate('RootFolderSelectFreeSpace', {
            freeSpace: formatBytes(freeSpace),
          })}
        </div>
      ) : null}

      {isMissing ? (
        <div className={IS_MISSING_CLASS}>{translate('Missing')}</div>
      ) : null}
    </EnhancedSelectInputSelectedValue>
  );
}

export default RootFolderSelectInputSelectedValue;
