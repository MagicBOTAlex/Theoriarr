import classNames from 'classnames';
import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import translate from 'Utilities/String/translate';
import { MediaActivityType } from './mediaActivity';
import { BadgeKind, StatusBadge } from './mediaActivityStatus';

export const ACTIVITY_TONE_CLASSES: Record<BadgeKind, string> = {
  default: 'bg-[var(--pageFooterBackground)] text-[var(--disabledColor)]',
  info: 'bg-[color-mix(in_srgb,var(--infoColor)_16%,transparent)] text-[var(--infoColor)]',
  primary:
    'bg-[color-mix(in_srgb,var(--themeBlue)_16%,transparent)] text-[var(--themeBlue)]',
  success:
    'bg-[color-mix(in_srgb,var(--successColor)_16%,transparent)] text-[var(--successColor)]',
  warning:
    'bg-[color-mix(in_srgb,var(--warningColor)_18%,transparent)] text-[var(--warningColor)]',
  danger:
    'bg-[color-mix(in_srgb,var(--dangerColor)_16%,transparent)] text-[var(--dangerColor)]',
  purple:
    'bg-[color-mix(in_srgb,var(--purple)_16%,transparent)] text-[var(--purple)]',
};

const TYPE_CLASSES: Record<MediaActivityType, string> = {
  series:
    'bg-[color-mix(in_srgb,var(--infoColor)_14%,transparent)] text-[var(--infoColor)]',
  movie:
    'bg-[color-mix(in_srgb,var(--purple)_14%,transparent)] text-[var(--purple)]',
};

const BADGE_CLASS =
  'inline-flex items-center gap-[6px] rounded-full px-[9px] py-[3px] text-[11px] font-semibold leading-none whitespace-nowrap';

interface MediaStatusBadgeProps extends StatusBadge {
  icon?: IconName;
}

export function MediaStatusBadge({ label, kind, icon }: MediaStatusBadgeProps) {
  return (
    <span className={classNames(BADGE_CLASS, ACTIVITY_TONE_CLASSES[kind])}>
      {icon ? (
        <Icon name={icon} size={11} aria-hidden={true} />
      ) : (
        <span className="h-[6px] w-[6px] rounded-full bg-current" />
      )}

      {label}
    </span>
  );
}

export function MediaTypeBadge({ type }: { type: MediaActivityType }) {
  return (
    <span className={classNames(BADGE_CLASS, TYPE_CLASSES[type])}>
      {type === 'series' ? translate('Show') : translate('Movie')}
    </span>
  );
}
