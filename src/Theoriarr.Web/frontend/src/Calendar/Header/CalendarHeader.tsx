import classNames from 'classnames';
import moment from 'moment-timezone';
import React, { useCallback, useMemo } from 'react';
import { useAppDimensions } from 'App/appStore';
import {
  setCalendarOption,
  useCalendarOption,
} from 'Calendar/calendarOptionsStore';
import { CalendarView } from 'Calendar/calendarViews';
import useCalendar, {
  goToDate,
  goToNextRange,
  goToPreviousRange,
  goToToday,
  useCalendarRange,
  useCalendarTime,
} from 'Calendar/useCalendar';
import DateInput, { DATE_INPUT_CLASS } from 'Components/DateInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator, {
  LOADING_INDICATOR_CLASS,
} from 'Components/Loading/LoadingIndicator';
import Menu, { MENU_CLASS } from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import ViewMenuItem from 'Components/Menu/ViewMenuItem';
import { align, icons } from 'Helpers/Props';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import translate from 'Utilities/String/translate';
import CalendarHeaderViewButton from './CalendarHeaderViewButton';

function CalendarHeader() {
  const { isFetching } = useCalendar();
  const view = useCalendarOption('view');
  const time = useCalendarTime();
  const { start, end } = useCalendarRange();

  const { isSmallScreen, isLargeScreen } = useAppDimensions();

  const { longDateFormat } = useUiSettingsValues();

  const handleViewChange = useCallback((newView: string) => {
    setCalendarOption('view', newView as CalendarView);
  }, []);

  const handleTodayPress = useCallback(() => {
    goToToday();
  }, []);

  const handlePreviousPress = useCallback(() => {
    goToPreviousRange();
  }, []);

  const handleNextPress = useCallback(() => {
    goToNextRange();
  }, []);

  const datePickerValue = useMemo(() => {
    if (view === 'month') {
      return moment(time).startOf('month').format('YYYY-MM-DD');
    }

    if (start) {
      return moment(start).format('YYYY-MM-DD');
    }

    return '';
  }, [view, time, start]);

  const title = useMemo(() => {
    const timeMoment = moment(time);
    const startMoment = moment(start);
    const endMoment = moment(end);

    if (view === 'day') {
      return timeMoment.format(longDateFormat);
    } else if (view === 'month') {
      return timeMoment.format('MMMM YYYY');
    } else if (view === 'agenda') {
      return translate('Agenda');
    }

    let startFormat = 'MMM D YYYY';
    let endFormat = 'MMM D YYYY';

    if (startMoment.isSame(endMoment, 'month')) {
      startFormat = 'MMM D';
      endFormat = 'D YYYY';
    } else if (startMoment.isSame(endMoment, 'year')) {
      startFormat = 'MMM D';
      endFormat = 'MMM D YYYY';
    }

    return `${startMoment.format(startFormat)} \u2014 ${endMoment.format(
      endFormat
    )}`;
  }, [time, start, end, view, longDateFormat]);

  return (
    <div>
      {isSmallScreen ? (
        <div className="text-center text-[18px] mb-[5px]">{title}</div>
      ) : null}

      <div className="flex">
        <div className="flex-[1_1_33%] text-left max-[768px]:flex-[1_0_50%]">
          <Button
            buttonGroupPosition="left"
            isDisabled={view === 'agenda'}
            onPress={handlePreviousPress}
          >
            <Icon name={icons.PAGE_PREVIOUS} />
          </Button>

          <Button
            buttonGroupPosition="right"
            isDisabled={view === 'agenda'}
            onPress={handleNextPress}
          >
            <Icon name={icons.PAGE_NEXT} />
          </Button>

          <Button
            className="ml-[5px]"
            isDisabled={view === 'agenda'}
            onPress={handleTodayPress}
          >
            {translate('Today')}
          </Button>

          <DateInput
            className={classNames(DATE_INPUT_CLASS, 'ml-[5px]')}
            value={datePickerValue}
            label={translate('GoToDate')}
            isDisabled={view === 'agenda'}
            onChange={goToDate}
          />
        </div>

        {isSmallScreen ? null : (
          <div className="text-center text-[18px]">{title}</div>
        )}

        <div className="flex justify-end flex-[1_1_33%] max-[768px]:flex-[0_0_100px]">
          {isFetching ? (
            <LoadingIndicator
              className={classNames(
                LOADING_INDICATOR_CLASS,
                'mt-[5px] mr-[10px]'
              )}
              size={20}
            />
          ) : null}

          {isLargeScreen ? (
            <Menu
              className={classNames(MENU_CLASS, 'leading-[31px]')}
              alignMenu={align.RIGHT}
            >
              <MenuButton>
                <Icon name={icons.VIEW} size={22} />
              </MenuButton>

              <MenuContent>
                {isSmallScreen ? null : (
                  <ViewMenuItem
                    name="month"
                    selectedView={view}
                    onPress={handleViewChange}
                  >
                    {translate('Month')}
                  </ViewMenuItem>
                )}

                <ViewMenuItem
                  name="week"
                  selectedView={view}
                  onPress={handleViewChange}
                >
                  {translate('Week')}
                </ViewMenuItem>

                <ViewMenuItem
                  name="forecast"
                  selectedView={view}
                  onPress={handleViewChange}
                >
                  {translate('Forecast')}
                </ViewMenuItem>

                <ViewMenuItem
                  name="day"
                  selectedView={view}
                  onPress={handleViewChange}
                >
                  {translate('Day')}
                </ViewMenuItem>

                <ViewMenuItem
                  name="agenda"
                  selectedView={view}
                  onPress={handleViewChange}
                >
                  {translate('Agenda')}
                </ViewMenuItem>
              </MenuContent>
            </Menu>
          ) : (
            <>
              <CalendarHeaderViewButton
                view="month"
                selectedView={view}
                buttonGroupPosition="left"
                onPress={handleViewChange}
              />

              <CalendarHeaderViewButton
                view="week"
                selectedView={view}
                buttonGroupPosition="center"
                onPress={handleViewChange}
              />

              <CalendarHeaderViewButton
                view="forecast"
                selectedView={view}
                buttonGroupPosition="center"
                onPress={handleViewChange}
              />

              <CalendarHeaderViewButton
                view="day"
                selectedView={view}
                buttonGroupPosition="center"
                onPress={handleViewChange}
              />

              <CalendarHeaderViewButton
                view="agenda"
                selectedView={view}
                buttonGroupPosition="right"
                onPress={handleViewChange}
              />
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default CalendarHeader;
