import React, { CSSProperties, LegacyRef, useId } from 'react';
import Scroller from 'Components/Scroller/Scroller';

export const MENU_CONTENT_CLASS =
  'z-[2000] flex flex-col bg-[var(--toolbarMenuItemBackgroundColor)] leading-[20px]';
export const MENU_CONTENT_SCROLLER_CLASS = 'flex flex-col';

interface MenuContentProps {
  forwardedRef?: LegacyRef<HTMLDivElement> | undefined;
  className?: string;
  id?: string;
  children: React.ReactNode;
  style?: CSSProperties;
  isOpen?: boolean;
}

function MenuContent({
  forwardedRef,
  className = MENU_CONTENT_CLASS,
  id,
  children,
  style,
  isOpen,
}: MenuContentProps) {
  const generatedId = useId();

  return (
    <div
      ref={forwardedRef}
      id={id ?? generatedId}
      className={className}
      style={style}
    >
      {isOpen ? (
        <Scroller className={MENU_CONTENT_SCROLLER_CLASS}>{children}</Scroller>
      ) : null}
    </div>
  );
}

export default MenuContent;
