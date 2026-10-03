import React, { ForwardedRef, forwardRef, ReactNode, useCallback } from 'react';
import Scroller, { OnScroll } from 'Components/Scroller/Scroller';
import useScrollPosition from 'Helpers/Hooks/useScrollPosition';
import { isLocked } from 'Utilities/scrollLock';

export const PAGE_CONTENT_BODY_CLASS =
  'flex-[1_0_1px] max-[768px]:flex-[1_0_auto] max-[768px]:overflow-y-hidden!';

export const PAGE_CONTENT_BODY_INNER_CLASS = 'p-[20px] max-[768px]:p-[10px]';

interface PageContentBodyProps {
  className?: string;
  innerClassName?: string;
  children: ReactNode;
  scrollPositionKey?: string;
  onScroll?: (payload: OnScroll) => void;
}

const PageContentBody = forwardRef(
  (props: PageContentBodyProps, ref: ForwardedRef<HTMLDivElement>) => {
    const {
      className = PAGE_CONTENT_BODY_CLASS,
      innerClassName = PAGE_CONTENT_BODY_INNER_CLASS,
      children,
      scrollPositionKey,
      onScroll,
    } = props;

    const { initialScrollTop, onScroll: onScrollMemo } =
      useScrollPosition(scrollPositionKey);

    const handleScroll = useCallback(
      (payload: OnScroll) => {
        if (isLocked()) {
          return;
        }

        onScrollMemo(payload);
        onScroll?.(payload);
      },
      [onScroll, onScrollMemo]
    );

    return (
      <Scroller
        ref={ref}
        className={className}
        scrollDirection="vertical"
        initialScrollTop={initialScrollTop}
        onScroll={handleScroll}
      >
        <div className={innerClassName}>{children}</div>
      </Scroller>
    );
  }
);

export default PageContentBody;
