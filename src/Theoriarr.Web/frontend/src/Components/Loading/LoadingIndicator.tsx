import React from 'react';

export const LOADING_INDICATOR_CLASS = 'mt-[20px] text-center';

const RIPPLE_CONTAINER_CLASS = 'relative inline-block';

export const RIPPLE_CLASS =
  'absolute border-2 border-solid border-[var(--themeDarkColor)] rounded-full [animation:rippleContainer_1.25s_0s_infinite_cubic-bezier(0.21,0.53,0.56,0.8)] [animation-fill-mode:both] [&:nth-child(1)]:[animation-delay:-0.6s] [&:nth-child(2)]:[animation-delay:-0.4s] [&:nth-child(3)]:[animation-delay:-0.2s]';

interface LoadingIndicatorProps {
  className?: string;
  rippleClassName?: string;
  size?: number;
}

function LoadingIndicator({
  className = LOADING_INDICATOR_CLASS,
  rippleClassName = RIPPLE_CLASS,
  size = 50,
}: LoadingIndicatorProps) {
  const sizeInPx = `${size}px`;
  const width = sizeInPx;
  const height = sizeInPx;

  return (
    <div className={className} style={{ height }}>
      <div className={RIPPLE_CONTAINER_CLASS} style={{ width, height }}>
        <div className={rippleClassName} style={{ width, height }} />

        <div className={rippleClassName} style={{ width, height }} />

        <div className={rippleClassName} style={{ width, height }} />
      </div>
    </div>
  );
}

export default LoadingIndicator;
