import classNames from 'classnames';
import React from 'react';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import TableRowCell from './TableRowCell';

interface RelativeDateCellProps {
  className?: string;
  date?: string;
  includeSeconds?: boolean;
  includeTime?: boolean;
  component?: React.ElementType;
}

function RelativeDateCell(props: RelativeDateCellProps) {
  const {
    className,
    date,
    includeSeconds = false,
    includeTime = false,
    component: Component = TableRowCell,
    ...otherProps
  } = props;

  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useUiSettingsValues();

  const cellClass = classNames('w-[180px]', className);

  if (!date) {
    return <Component className={cellClass} {...otherProps} />;
  }

  return (
    <Component
      className={cellClass}
      title={formatDateTime(date, longDateFormat, timeFormat, {
        includeSeconds,
        includeRelativeDay: !showRelativeDates,
      })}
      {...otherProps}
    >
      {getRelativeDate({
        date,
        shortDateFormat,
        showRelativeDates,
        timeFormat,
        includeSeconds,
        includeTime,
        timeForToday: true,
      })}
    </Component>
  );
}

export default RelativeDateCell;
