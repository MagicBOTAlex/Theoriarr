import classNames from 'classnames';
import React from 'react';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import EnhancedSelectInputOption, {
  EnhancedSelectInputOptionProps,
} from './EnhancedSelectInputOption';

interface RootFolderSelectInputOptionProps
  extends EnhancedSelectInputOptionProps {
  id: string;
  value: string;
  freeSpace?: number;
  isMissing?: boolean;
  seriesFolder?: string;
  isMobile: boolean;
  isWindows?: boolean;
}

function RootFolderSelectInputOption({
  id,
  value,
  freeSpace,
  isMissing,
  seriesFolder,
  isMobile,
  isWindows,
  ...otherProps
}: RootFolderSelectInputOptionProps) {
  const slashCharacter = isWindows ? '\\' : '/';

  return (
    <EnhancedSelectInputOption id={id} isMobile={isMobile} {...otherProps}>
      <div
        className={classNames(
          'flex flex-[1_0_0] items-center justify-between',
          isMobile && 'block'
        )}
      >
        <div className="flex">
          {value}

          {seriesFolder && id !== 'addNew' ? (
            <div className="flex-none text-[var(--disabledColor)]">
              {slashCharacter}
              {seriesFolder}
            </div>
          ) : null}
        </div>

        {freeSpace == null ? null : (
          <div
            className={classNames(
              'ml-[15px] text-[12px] text-[var(--darkGray)]',
              isMobile && 'ml-0'
            )}
          >
            {translate('RootFolderSelectFreeSpace', {
              freeSpace: formatBytes(freeSpace),
            })}
          </div>
        )}

        {isMissing ? (
          <div className="ml-[15px] text-[12px] text-[var(--dangerColor)]">
            {translate('Missing')}
          </div>
        ) : null}
      </div>
    </EnhancedSelectInputOption>
  );
}

export default RootFolderSelectInputOption;
