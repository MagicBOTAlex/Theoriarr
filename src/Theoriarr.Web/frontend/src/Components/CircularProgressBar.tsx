import React, { useCallback, useEffect, useState } from 'react';

const CIRCULAR_PROGRESS_BAR_CLASS =
  'absolute top-0 left-0 [transform:rotate(-90deg)] [transform-origin:center_center]';

const CONTAINER_CLASS = 'relative inline-block text-center align-top';

const TEXT_CLASS = 'absolute h-full w-full font-bold';

interface CircularProgressBarProps {
  className?: string;
  containerClassName?: string;
  size?: number;
  progress: number;
  strokeWidth?: number;
  strokeColor?: string;
  showProgressText?: boolean;
}

function CircularProgressBar({
  className = CIRCULAR_PROGRESS_BAR_CLASS,
  containerClassName = CONTAINER_CLASS,
  size = 60,
  strokeWidth = 5,
  strokeColor = '#35c5f4',
  showProgressText = false,
  progress,
}: CircularProgressBarProps) {
  const [currentProgress, setCurrentProgress] = useState(0);
  const raf = React.useRef<number>(0);
  const center = size / 2;
  const radius = center - strokeWidth;
  const circumference = Math.PI * (radius * 2);
  const sizeInPixels = `${size}px`;
  const strokeDashoffset = ((100 - currentProgress) / 100) * circumference;
  const progressText = `${Math.round(currentProgress)}%`;

  const handleAnimation = useCallback(
    (p: number) => {
      setCurrentProgress((prevProgress) => {
        if (prevProgress < p) {
          return prevProgress + Math.min(1, p - prevProgress);
        }

        return prevProgress;
      });
    },
    [setCurrentProgress]
  );

  useEffect(() => {
    if (progress > currentProgress) {
      cancelAnimationFrame(raf.current);

      raf.current = requestAnimationFrame(() => handleAnimation(progress));
    }
  }, [progress, currentProgress, handleAnimation]);

  useEffect(
    () => {
      return () => cancelAnimationFrame(raf.current);
    },
    // We only want to run this effect once
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  return (
    <div
      className={containerClassName}
      style={{
        width: sizeInPixels,
        height: sizeInPixels,
        lineHeight: sizeInPixels,
      }}
    >
      <svg
        className={className}
        version="1.1"
        xmlns="http://www.w3.org/2000/svg"
        width={size}
        height={size}
      >
        <circle
          fill="transparent"
          r={radius}
          cx={center}
          cy={center}
          strokeDasharray={circumference}
          style={{
            stroke: strokeColor,
            strokeWidth,
            strokeDashoffset,
          }}
        />
      </svg>

      {showProgressText && <div className={TEXT_CLASS}>{progressText}</div>}
    </div>
  );
}

export default CircularProgressBar;
