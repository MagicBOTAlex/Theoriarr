import classNames from 'classnames';
import React from 'react';
import EnhancedSelectInputOption, {
  EnhancedSelectInputOptionProps,
} from './EnhancedSelectInputOption';

interface SeriesTypeSelectInputOptionProps
  extends EnhancedSelectInputOptionProps {
  id: string;
  value: string;
  format: string;
  isMobile: boolean;
}

function SeriesTypeSelectInputOption(props: SeriesTypeSelectInputOptionProps) {
  const { id, value, format, isMobile, ...otherProps } = props;

  return (
    <EnhancedSelectInputOption {...otherProps} id={id} isMobile={isMobile}>
      <div
        className={classNames(
          'flex flex-[1_0_0] items-center justify-between',
          isMobile && 'block'
        )}
      >
        <div className="flex">{value}</div>

        {format == null ? null : (
          <div
            className={classNames(
              'ml-[15px] text-[12px] text-[var(--darkGray)]',
              isMobile && 'ml-0'
            )}
          >
            {format}
          </div>
        )}
      </div>
    </EnhancedSelectInputOption>
  );
}

export default SeriesTypeSelectInputOption;
