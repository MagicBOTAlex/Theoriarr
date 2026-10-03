import classNames from 'classnames';
import { throttle } from 'lodash';
import React, {
  ComponentProps,
  ForwardedRef,
  forwardRef,
  ReactNode,
  useEffect,
  useImperativeHandle,
  useRef,
} from 'react';
import { ScrollDirection } from 'Helpers/Props/scrollDirections';

const SCROLLER_CLASS =
  '[scrollbar-color:var(--scrollbarBackgroundColor)_transparent] [scrollbar-width:thin] [-webkit-overflow-scrolling:touch] [&::-webkit-scrollbar]:w-[10px] [&::-webkit-scrollbar]:h-[10px] [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:min-h-[100px] [&::-webkit-scrollbar-thumb]:border [&::-webkit-scrollbar-thumb]:border-solid [&::-webkit-scrollbar-thumb]:border-transparent [&::-webkit-scrollbar-thumb]:rounded-[5px] [&::-webkit-scrollbar-thumb]:bg-[var(--scrollbarBackgroundColor)] [&::-webkit-scrollbar-thumb]:[background-clip:padding-box] [&::-webkit-scrollbar-thumb:hover]:bg-[var(--scrollbarHoverBackgroundColor)]';

const DIRECTION_CLASSES: Record<ScrollDirection, string> = {
  none: 'overflow-hidden',
  vertical: 'overflow-x-hidden overflow-y-scroll',
  horizontal: 'overflow-x-scroll overflow-y-hidden',
  both: 'overflow-scroll',
};

const AUTO_SCROLL_DIRECTION_CLASSES: Record<ScrollDirection, string> = {
  none: 'overflow-hidden',
  vertical: 'overflow-x-hidden overflow-y-auto',
  horizontal: 'overflow-x-auto overflow-y-hidden',
  both: 'overflow-auto',
};

export interface OnScroll {
  scrollLeft: number;
  scrollTop: number;
}

interface ScrollerProps {
  className?: string;
  scrollDirection?: ScrollDirection;
  autoFocus?: boolean;
  autoScroll?: boolean;
  scrollTop?: number;
  initialScrollTop?: number;
  children?: ReactNode;
  style?: ComponentProps<'div'>['style'];
  onScroll?: (payload: OnScroll) => void;
}

const Scroller = forwardRef(
  (props: ScrollerProps, ref: ForwardedRef<HTMLDivElement>) => {
    const {
      className,
      autoFocus = false,
      autoScroll = true,
      scrollDirection = 'vertical',
      children,
      scrollTop,
      initialScrollTop,
      onScroll,
      ...otherProps
    } = props;

    const internalRef = useRef<HTMLDivElement>(null);

    useImperativeHandle(ref, () => internalRef.current!, []);

    useEffect(
      () => {
        if (initialScrollTop != null) {
          internalRef.current!.scrollTop = initialScrollTop;
        }
      },
      // eslint-disable-next-line react-hooks/exhaustive-deps
      []
    );

    useEffect(() => {
      if (scrollTop != null) {
        internalRef.current!.scrollTop = scrollTop;
      }

      if (autoFocus && scrollDirection !== 'none') {
        internalRef.current!.focus({ preventScroll: true });
      }
    }, [autoFocus, scrollDirection, scrollTop]);

    useEffect(() => {
      const div = internalRef.current!;

      const handleScroll = throttle(() => {
        const scrollLeft = div.scrollLeft;
        const scrollTop = div.scrollTop;

        onScroll?.({ scrollLeft, scrollTop });
      }, 10);

      div?.addEventListener('scroll', handleScroll);

      return () => {
        div?.removeEventListener('scroll', handleScroll);
      };
    }, [onScroll]);

    return (
      <div
        {...otherProps}
        ref={internalRef}
        className={classNames(
          className,
          SCROLLER_CLASS,
          autoScroll
            ? AUTO_SCROLL_DIRECTION_CLASSES[scrollDirection]
            : DIRECTION_CLASSES[scrollDirection]
        )}
        tabIndex={-1}
      >
        {children}
      </div>
    );
  }
);

export default Scroller;
