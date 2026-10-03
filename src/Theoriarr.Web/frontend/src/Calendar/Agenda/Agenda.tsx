import moment from 'moment-timezone';
import React from 'react';
import useCalendar from 'Calendar/useCalendar';
import AgendaEvent from './AgendaEvent';

function Agenda() {
  const { data } = useCalendar();

  return (
    <div className="mt-[10px]">
      {data.map((item, index) => {
        const momentDate = moment(item.airDateUtc);
        const showDate =
          index === 0 ||
          !moment(data[index - 1].airDateUtc).isSame(momentDate, 'day');

        return <AgendaEvent key={item.id} showDate={showDate} {...item} />;
      })}
    </div>
  );
}

export default Agenda;
