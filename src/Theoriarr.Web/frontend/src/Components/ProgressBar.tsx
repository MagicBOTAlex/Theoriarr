import classNames from 'classnames';
import React from 'react';
import { ColorImpairedConsumer } from 'App/ColorImpairedContext';
import { Kind } from 'Helpers/Props/kinds';
import { Size } from 'Helpers/Props/sizes';
import translate from 'Utilities/String/translate';

export const PROGRESS_BAR_CONTAINER_CLASS =
  'relative overflow-hidden w-full rounded-[4px] bg-[var(--progressBarBackgroundColor)] shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)]';

export const PROGRESS_BAR_CLASS =
  'relative z-[1] float-left w-0 h-full shadow-[inset_0_-1px_0_rgba(0,0,0,0.15)] text-[var(--white)] transition-[width_0.6s_ease]';

const SIZE_CLASSES = {
  small: 'h-[5px]',
  medium: 'h-[15px]',
  large: 'h-[20px]',
};

const KIND_CLASSES = {
  danger:
    'bg-[var(--dangerColor)] [&.colorImpaired]:[background:repeating-linear-gradient(90deg,rgb(228,76,76),rgb(228,76,76)_5px,rgb(204,68,68)_5px,rgb(204,68,68)_10px)]',
  info: 'bg-[var(--infoColor)]',
  primary: 'bg-[var(--primaryColor)]',
  purple: 'bg-[var(--purple)]',
  success: 'bg-[var(--successColor)]',
  warning:
    'bg-[var(--warningColor)] [&.colorImpaired]:[background:repeating-linear-gradient(45deg,#ffa500,#ffa500_5px,rgb(255,179,38)_5px,rgb(255,179,38)_10px)]',
};

const TEXT_CONTAINER_CLASS = 'absolute overflow-hidden w-0 h-full';
const TEXT_CLASS =
  'flex items-center justify-center text-center text-[12px] cursor-default h-full';

const PARTS_CONTAINER_CLASS =
  'relative z-[1] flex w-full h-full overflow-hidden';

export interface ProgressBarPart {
  kind: Extract<Kind, keyof typeof KIND_CLASSES>;
  value: number;
  title?: string;
}

interface ProgressBarProps {
  ariaLabel?: string;
  className?: string;
  containerClassName?: string;
  title?: string;
  progress: number;
  precision?: number;
  showText?: boolean;
  text?: string;
  kind?: Extract<Kind, keyof typeof KIND_CLASSES>;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  width?: number;
  parts?: ProgressBarPart[];
}

function ProgressBar({
  ariaLabel,
  className = PROGRESS_BAR_CLASS,
  containerClassName = PROGRESS_BAR_CONTAINER_CLASS,
  title,
  progress,
  precision = 1,
  showText = false,
  text,
  kind = 'primary',
  size = 'medium',
  width,
  parts,
}: ProgressBarProps) {
  const progressPercent = `${progress.toFixed(precision)}%`;
  const progressText = text || progressPercent;
  const actualWidth = width ? `${width}px` : '100%';
  const hasParts = !!parts && parts.length > 0;

  return (
    <ColorImpairedConsumer>
      {(enableColorImpairedMode) => {
        return (
          <div
            className={classNames(containerClassName, SIZE_CLASSES[size])}
            title={title}
            style={{ width: actualWidth }}
          >
            {showText && width ? (
              <div
                className={classNames(
                  TEXT_CONTAINER_CLASS,
                  'text-[var(--progressBarBackTextColor)]'
                )}
                style={{ width: actualWidth }}
              >
                <div className={TEXT_CLASS}>
                  <div>{progressText}</div>
                </div>
              </div>
            ) : null}

            {hasParts ? (
              <div
                className={PARTS_CONTAINER_CLASS}
                role="meter"
                aria-label={
                  ariaLabel ??
                  translate('ProgressBarProgress', {
                    progress: progress.toFixed(0),
                  })
                }
                aria-valuenow={Math.floor(progress)}
                aria-valuemin={0}
                aria-valuemax={100}
              >
                {parts.map((part, index) => (
                  <div
                    key={index}
                    className={classNames(
                      'h-full transition-[width_0.6s_ease]',
                      KIND_CLASSES[part.kind],
                      enableColorImpairedMode && 'colorImpaired'
                    )}
                    style={{ width: `${part.value}%` }}
                  />
                ))}
              </div>
            ) : (
              <div
                className={classNames(
                  className,
                  KIND_CLASSES[kind],
                  enableColorImpairedMode && 'colorImpaired'
                )}
                role="meter"
                aria-label={
                  ariaLabel ??
                  translate('ProgressBarProgress', {
                    progress: progress.toFixed(0),
                  })
                }
                aria-valuenow={Math.floor(progress)}
                aria-valuemin={0}
                aria-valuemax={100}
                style={{ width: progressPercent }}
              />
            )}

            {showText ? (
              <div
                className={classNames(
                  TEXT_CONTAINER_CLASS,
                  'z-[1] text-[var(--progressBarFrontTextColor)]'
                )}
                style={{ width: hasParts ? actualWidth : progressPercent }}
              >
                <div className={TEXT_CLASS} style={{ width: actualWidth }}>
                  <div>{progressText}</div>
                </div>
              </div>
            ) : null}
          </div>
        );
      }}
    </ColorImpairedConsumer>
  );
}

export default ProgressBar;
