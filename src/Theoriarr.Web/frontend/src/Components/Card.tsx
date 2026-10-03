import classNames from 'classnames';
import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';

const CARD_CLASS =
  'relative m-[10px] p-[10px] rounded-[3px] bg-[var(--cardBackgroundColor)] shadow-[0_0_10px_1px_var(--cardShadowColor)] text-[var(--defaultColor)]';

const UNDERLAY_CLASS = 'absolute top-0 left-0 block w-full h-full';

const OVERLAY_CLASS =
  'relative top-0 left-0 block w-full h-full pointer-events-none select-none [&_a]:pointer-events-auto [&_button]:pointer-events-auto';

interface CardProps
  extends Pick<LinkProps, 'aria-label' | 'onPress' | 'title'> {
  // TODO: Consider using different properties for classname depending if it's overlaying content or not
  className?: string;
  overlayClassName?: string;
  overlayContent?: boolean;
  children: React.ReactNode;
}

function Card(props: CardProps) {
  const {
    className,
    overlayClassName,
    overlayContent = false,
    children,
    'aria-label': ariaLabel,
    onPress,
    title,
  } = props;

  const cardClass = classNames(CARD_CLASS, className);
  const overlayClass = overlayClassName ?? OVERLAY_CLASS;

  if (overlayContent) {
    return (
      <div className={cardClass}>
        <Link
          className={UNDERLAY_CLASS}
          aria-label={ariaLabel}
          title={title}
          onPress={onPress}
        />

        <div className={overlayClass}>{children}</div>
      </div>
    );
  }

  return (
    <Link
      className={cardClass}
      aria-label={ariaLabel}
      title={title}
      onPress={onPress}
    >
      {children}
    </Link>
  );
}

export default Card;
