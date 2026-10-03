import React from 'react';
import Scroller from 'Components/Scroller/Scroller';
import { ScrollDirection } from 'Helpers/Props/scrollDirections';

export const MODAL_BODY_CLASS = 'flex-[1_0_1px] p-[30px]';

export const MODAL_BODY_SCROLLER_CLASS = 'grow';

export const MODAL_BODY_INNER_CLASS = 'p-[30px]';

interface ModalBodyProps {
  className?: string;
  innerClassName?: string;
  children?: React.ReactNode;
  scrollDirection?: ScrollDirection;
}

function ModalBody({
  innerClassName = MODAL_BODY_INNER_CLASS,
  scrollDirection = 'vertical',
  children,
  ...otherProps
}: ModalBodyProps) {
  const hasScroller = scrollDirection !== 'none';

  const className =
    otherProps.className ??
    (hasScroller ? MODAL_BODY_SCROLLER_CLASS : MODAL_BODY_CLASS);

  return (
    <Scroller
      {...otherProps}
      className={className}
      scrollDirection={scrollDirection}
      scrollTop={0}
    >
      {hasScroller ? (
        <div className={innerClassName}>{children}</div>
      ) : (
        children
      )}
    </Scroller>
  );
}

export default ModalBody;
