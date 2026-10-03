import {
  arrow,
  autoUpdate,
  flip,
  FloatingArrow,
  FloatingFocusManager,
  FloatingPortal,
  offset,
  Placement,
  safePolygon,
  shift,
  useClick,
  useDismiss,
  useFloating,
  useFocus,
  useHover,
  useInteractions,
  useRole,
} from '@floating-ui/react';
import classNames from 'classnames';
import React, { useRef, useState } from 'react';
import { useThemeColor } from 'Helpers/Hooks/useTheme';
import { kinds } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import { isMobile } from 'Utilities/browser';

const KIND_CLASSES: Record<'default' | 'inverse', string> = {
  default:
    'bg-[var(--popoverBodyBackgroundColor)] shadow-[0_5px_10px_var(--popoverShadowColor)]',
  inverse:
    'bg-[var(--themeDarkColor)] text-white shadow-[0_5px_10px_var(--popoverShadowInverseColor)]',
};

const TOOLTIP_MAX_WIDTH =
  'min-[480px]:max-w-[calc(768px*0.8)] min-[768px]:max-w-[calc(992px*0.8)] min-[992px]:max-w-[calc(1200px*0.8)]';

export interface TooltipProps {
  accessibleLabel?: string;
  className?: string;
  bodyClassName?: string;
  anchor: React.ReactNode;
  tooltip: string | React.ReactNode;
  contentRole?: 'dialog' | 'tooltip';
  isAnchorFocusable?: boolean;
  kind?: Extract<Kind, 'default' | 'inverse'>;
  position?: Placement;
  canFlip?: boolean;
}

function Tooltip(props: TooltipProps) {
  const {
    accessibleLabel,
    className,
    bodyClassName = 'p-[5px]',
    anchor,
    tooltip,
    contentRole = 'tooltip',
    isAnchorFocusable = true,
    kind = kinds.DEFAULT,
    position,
    canFlip = true,
  } = props;

  const arrowColor = useThemeColor(
    kind === 'inverse'
      ? 'popoverArrowBorderInverseColor'
      : 'popoverArrowBorderColor'
  );
  const [isOpen, setIsOpen] = useState(false);

  const arrowRef = useRef(null);

  const { refs, context, floatingStyles } = useFloating({
    middleware: [
      arrow({
        element: arrowRef,
      }),
      flip({
        crossAxis: canFlip,
        mainAxis: canFlip,
      }),
      offset({ mainAxis: 10 }),
      shift(),
    ],
    open: isOpen,
    placement: position,
    whileElementsMounted: autoUpdate,
    onOpenChange: setIsOpen,
  });

  const click = useClick(context, {
    enabled: isMobile() || contentRole === 'dialog',
  });
  const dismiss = useDismiss(context);
  const focus = useFocus(context, {
    enabled: contentRole === 'tooltip',
  });
  const hover = useHover(context, {
    handleClose: safePolygon(),
  });
  const role = useRole(context, { role: contentRole });

  const { getReferenceProps, getFloatingProps } = useInteractions([
    click,
    dismiss,
    focus,
    hover,
    role,
  ]);

  const floatingContent = (
    <div
      ref={refs.setFloating}
      className="z-[2000]"
      style={floatingStyles}
      {...getFloatingProps()}
    >
      <FloatingArrow ref={arrowRef} context={context} fill={arrowColor} />
      <div
        className={classNames(
          'relative',
          TOOLTIP_MAX_WIDTH,
          KIND_CLASSES[kind]
        )}
      >
        <div className={bodyClassName}>{tooltip}</div>
      </div>
    </div>
  );

  return (
    <>
      <span
        ref={refs.setReference}
        {...getReferenceProps({
          'aria-label': accessibleLabel,
          role: contentRole === 'dialog' ? 'button' : undefined,
          tabIndex: isAnchorFocusable ? 0 : undefined,
        })}
        className={classNames(
          'focus-visible:[outline:2px_solid_var(--linkColor)] focus-visible:outline-offset-2',
          className
        )}
      >
        {anchor}
      </span>
      {isOpen ? (
        <FloatingPortal id="portal-root">
          {contentRole === 'dialog' ? (
            <FloatingFocusManager
              context={context}
              initialFocus={-1}
              modal={false}
              order={['reference', 'content']}
            >
              {floatingContent}
            </FloatingFocusManager>
          ) : (
            floatingContent
          )}
        </FloatingPortal>
      ) : null}
    </>
  );
}

export default Tooltip;
