import classNames from 'classnames';
import React, { useEffect, useMemo, useRef } from 'react';
import { hideMessage, MessageType } from 'App/messagesStore';
import Icon, { IconName } from 'Components/Icon';
import { icons } from 'Helpers/Props';

const MESSAGE_CLASS = 'flex border-l-[3px] border-l-[var(--infoColor)]';

const TYPE_CLASSES: Record<MessageType, string> = {
  error: 'border-l-[var(--dangerColor)]',
  info: 'border-l-[var(--infoColor)]',
  success: 'border-l-[var(--successColor)]',
  warning: 'border-l-[var(--warningColor)]',
};

interface MessageProps {
  id: number;
  hideAfter: number;
  name: string;
  message: string;
  type: MessageType;
}

function Message({ id, hideAfter, name, message, type }: MessageProps) {
  const dismissTimeout = useRef<ReturnType<typeof setTimeout>>();
  const isError = type === 'error';

  const icon: IconName = useMemo(() => {
    switch (name) {
      case 'ApplicationUpdate':
        return icons.RESTART;
      case 'Backup':
        return icons.BACKUP;
      case 'CheckHealth':
        return icons.HEALTH;
      case 'EpisodeSearch':
        return icons.SEARCH;
      case 'Housekeeping':
        return icons.HOUSEKEEPING;
      case 'RefreshSeries':
        return icons.REFRESH;
      case 'RssSync':
        return icons.RSS;
      case 'SeasonSearch':
        return icons.SEARCH;
      case 'SeriesSearch':
        return icons.SEARCH;
      case 'UpdateSceneMapping':
        return icons.REFRESH;
      default:
        return icons.SPINNER;
    }
  }, [name]);

  useEffect(() => {
    if (hideAfter) {
      dismissTimeout.current = setTimeout(() => {
        hideMessage({ id });

        dismissTimeout.current = undefined;
      }, hideAfter * 1000);
    }

    return () => {
      if (dismissTimeout.current) {
        clearTimeout(dismissTimeout.current);
      }
    };
  }, [id, hideAfter, message, type]);

  return (
    <div
      className={classNames(MESSAGE_CLASS, TYPE_CLASSES[type])}
      role={isError ? 'alert' : 'status'}
      aria-atomic={true}
    >
      <div className="flex flex-[0_0_25px] flex-col justify-center py-2.5 pl-6 text-[var(--sidebarColor)]">
        <Icon name={icon} aria-hidden={true} />
      </div>

      <div className="mr-6 flex flex-col justify-center py-0.5 text-[13px] text-[var(--sidebarColor)]">
        {message}
      </div>
    </div>
  );
}

export default Message;
