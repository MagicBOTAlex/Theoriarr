import { FontAwesomeIconProps } from '@fortawesome/react-fontawesome';
import classNames from 'classnames';
import React from 'react';
import Icon, { IconProps } from 'Components/Icon';

const ICON_CLASS =
  'mr-[5px] [&.fullColorEvents]:[filter:var(--calendarFullColorFilter)]';

interface LegendIconItemProps extends Pick<IconProps, 'kind'> {
  name: string;
  fullColorEvents: boolean;
  icon: FontAwesomeIconProps['icon'];
  tooltip: string;
}

function LegendIconItem(props: LegendIconItemProps) {
  const { name, fullColorEvents, icon, kind, tooltip } = props;

  return (
    <div className="my-[3px] mr-[6px] w-[150px] cursor-default" title={tooltip}>
      <Icon
        className={classNames(ICON_CLASS, fullColorEvents && 'fullColorEvents')}
        name={icon}
        kind={kind}
      />

      {name}
    </div>
  );
}

export default LegendIconItem;
