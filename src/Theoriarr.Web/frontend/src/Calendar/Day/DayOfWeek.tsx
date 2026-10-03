import classNames from 'classnames';
import moment from 'moment-timezone';
import React from 'react';
import * as calendarViews from 'Calendar/calendarViews';
import getRelativeDate from 'Utilities/Date/getRelativeDate';

const DAY_OF_WEEK_CLASS =
  'grow shrink-0 basis-[14.28%] bg-[var(--calendarBackgroundColor)] text-center';

const IS_TODAY_CLASS = 'bg-[var(--calendarTodayBackgroundColor)]!';

interface DayOfWeekProps {
  date: string;
  view: string;
  isTodaysDate: boolean;
  calendarWeekColumnHeader: string;
  shortDateFormat: string;
  showRelativeDates: boolean;
}

function DayOfWeek(props: DayOfWeekProps) {
  const {
    date,
    view,
    isTodaysDate,
    calendarWeekColumnHeader,
    shortDateFormat,
    showRelativeDates,
  } = props;

  const highlightToday = view !== calendarViews.MONTH && isTodaysDate;
  const momentDate = moment(date);
  let formatedDate = momentDate.format('dddd');

  if (view === calendarViews.WEEK) {
    formatedDate = momentDate.format(calendarWeekColumnHeader);
  } else if (view === calendarViews.FORECAST) {
    formatedDate = getRelativeDate({
      date,
      shortDateFormat,
      showRelativeDates,
    });
  }

  return (
    <div
      className={classNames(
        DAY_OF_WEEK_CLASS,
        view === calendarViews.DAY && 'w-full',
        highlightToday && IS_TODAY_CLASS
      )}
    >
      {formatedDate}
    </div>
  );
}

export default DayOfWeek;
