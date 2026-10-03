import classNames from 'classnames';
import React from 'react';
import { Kind } from 'Helpers/Props/kinds';

export const ALERT_CLASS =
  'block m-[5px] p-[15px] border border-solid border-transparent rounded-[4px]';

const KIND_CLASSES = {
  danger:
    'border-[var(--alertDangerBorderColor)] bg-[var(--alertDangerBackgroundColor)] text-[var(--alertDangerColor)]',
  info: 'border-[var(--alertInfoBorderColor)] bg-[var(--alertInfoBackgroundColor)] text-[var(--alertInfoColor)]',
  success:
    'border-[var(--alertSuccessBorderColor)] bg-[var(--alertSuccessBackgroundColor)] text-[var(--alertSuccessColor)]',
  warning:
    'border-[var(--alertWarningBorderColor)] bg-[var(--alertWarningBackgroundColor)] text-[var(--alertWarningColor)]',
};

type AlertKind = Extract<Kind, keyof typeof KIND_CLASSES>;

interface AlertProps {
  className?: string;
  kind?: AlertKind;
  children: React.ReactNode;
}

function Alert(props: AlertProps) {
  const { className = ALERT_CLASS, kind = 'info', children } = props;

  return (
    <div className={classNames(className, KIND_CLASSES[kind])}>{children}</div>
  );
}

export default Alert;
