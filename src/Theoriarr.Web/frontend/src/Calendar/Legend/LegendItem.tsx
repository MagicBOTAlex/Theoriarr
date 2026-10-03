import classNames from 'classnames';
import React from 'react';
import { CALENDAR_EVENT_STATUS_CLASSES } from 'Calendar/Events/CalendarEvent';
import { CalendarStatus } from 'typings/Calendar';
import titleCase from 'Utilities/String/titleCase';

const LEGEND_ITEM_CLASS =
  'm-[3px_0] mr-[6px] pl-[5px] w-[150px] border-l-4 [border-left-style:solid] cursor-default';

interface LegendItemProps {
  name?: string;
  status: CalendarStatus;
  tooltip: string;
  isAgendaView: boolean;
  fullColorEvents: boolean;
  colorImpairedMode: boolean;
}

function LegendItem(props: LegendItemProps) {
  const {
    name,
    status,
    tooltip,
    isAgendaView,
    fullColorEvents,
    colorImpairedMode,
  } = props;

  return (
    <div
      className={classNames(
        LEGEND_ITEM_CLASS,
        CALENDAR_EVENT_STATUS_CLASSES[status],
        colorImpairedMode && 'colorImpaired',
        fullColorEvents && !isAgendaView && 'fullColor'
      )}
      title={tooltip}
    >
      {name ? name : titleCase(status)}
    </div>
  );
}

export default LegendItem;
