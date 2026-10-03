import classNames from 'classnames';
import elementClass from 'element-class';
import React, {
  MouseEvent,
  useCallback,
  useEffect,
  useId,
  useRef,
} from 'react';
import ReactDOM from 'react-dom';
import FocusLock from 'react-focus-lock';
import ErrorBoundary from 'Components/Error/ErrorBoundary';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { Size } from 'Helpers/Props/sizes';
import { isIOS } from 'Utilities/browser';
import * as keyCodes from 'Utilities/Constants/keyCodes';
import { setScrollLock } from 'Utilities/scrollLock';
import { ModalContext } from './ModalContext';
import ModalError from './ModalError';

export const MODAL_CONTAINER_CLASS =
  'absolute top-0 z-[1000] w-full h-full max-[992px]:fixed';

export const MODAL_BACKDROP_CLASS =
  'flex items-center justify-center w-full h-full bg-[var(--modalBackdropBackgroundColor)] backdrop-blur-[2px] opacity-100';

export const MODAL_CLASS =
  'relative flex max-w-[90%] max-h-[90%] overflow-hidden rounded-xl opacity-100 shadow-[0_20px_60px_-15px_rgba(0,0,0,0.6)] ring-1 ring-[color-mix(in_srgb,var(--textColor),transparent_88%)]';

export const MODAL_OPEN_CLASS = 'overflow-hidden!';

export const MODAL_SIZE_CLASSES: Record<Size, string> = {
  extraSmall: 'w-[320px]',
  small:
    'w-[480px] max-[992px]:w-full max-[992px]:h-full! max-[992px]:max-h-full',
  medium:
    'w-[720px] max-[992px]:w-full max-[992px]:h-full! max-[992px]:max-h-full',
  large:
    'w-[1080px] max-[1200px]:w-[90%] max-[992px]:w-full max-[992px]:h-full! max-[992px]:max-h-full',
  extraLarge:
    'w-[1280px] max-[1450px]:w-[90%] max-[992px]:w-full max-[992px]:h-full! max-[992px]:max-h-full',
  extraExtraLarge:
    'w-[1600px] max-[992px]:w-full max-[992px]:h-full! max-[992px]:max-h-full',
};

const openModals: string[] = [];
const node = document.getElementById('portal-root');

function removeFromOpenModals(id: string) {
  const index = openModals.indexOf(id);

  if (index >= 0) {
    openModals.splice(index, 1);
  }
}

function findEventTarget(event: TouchEvent | MouseEvent) {
  if ('changedTouches' in event) {
    const changedTouches = event.changedTouches;

    if (!changedTouches) {
      return event.target;
    }

    if (changedTouches.length === 1) {
      const touch = changedTouches[0];

      return document.elementFromPoint(touch.clientX, touch.clientY);
    }
  }

  return event.target;
}

export interface ModalProps {
  className?: string;
  style?: object;
  backdropClassName?: string;
  size?: Size;
  children?: React.ReactNode;
  isOpen: boolean;
  closeOnBackgroundClick?: boolean;
  onModalClose: () => void;
}

function Modal({
  className = MODAL_CLASS,
  style,
  backdropClassName = MODAL_BACKDROP_CLASS,
  size = 'large',
  children,
  isOpen,
  closeOnBackgroundClick = true,
  onModalClose,
}: ModalProps) {
  const backgroundRef = useRef<HTMLDivElement>(null);
  const isBackdropPressed = useRef(false);
  const wasOpen = usePrevious(isOpen);
  const modalId = useId();

  const isTargetBackdrop = useCallback((event: TouchEvent | MouseEvent) => {
    const targetElement = findEventTarget(event);

    if (targetElement) {
      return backgroundRef.current?.isEqualNode(targetElement as Node) ?? false;
    }

    return false;
  }, []);

  const handleBackdropBeginPress = useCallback(
    (event: MouseEvent<HTMLDivElement>) => {
      isBackdropPressed.current = isTargetBackdrop(event);
    },
    [isTargetBackdrop]
  );

  const handleBackdropEndPress = useCallback(
    (event: MouseEvent<HTMLDivElement>) => {
      if (
        isBackdropPressed.current &&
        isTargetBackdrop(event) &&
        closeOnBackgroundClick
      ) {
        onModalClose();
      }

      isBackdropPressed.current = false;
    },
    [closeOnBackgroundClick, isTargetBackdrop, onModalClose]
  );

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      if (event.keyCode === keyCodes.ESCAPE) {
        if (openModals.indexOf(modalId) === openModals.length - 1) {
          event.preventDefault();
          event.stopPropagation();

          onModalClose();
        }
      }
    },
    [modalId, onModalClose]
  );

  useEffect(() => {
    if (isOpen && !wasOpen) {
      openModals.push(modalId);

      if (openModals.length === 1) {
        if (isIOS()) {
          setScrollLock(true);
        } else {
          elementClass(document.body).add(MODAL_OPEN_CLASS);
        }
      }
    } else if (!isOpen && wasOpen) {
      removeFromOpenModals(modalId);

      if (openModals.length === 0) {
        if (isIOS()) {
          setScrollLock(false);
        } else {
          elementClass(document.body).remove(MODAL_OPEN_CLASS);
        }
      }
    }
  }, [isOpen, wasOpen, modalId, handleKeyDown]);

  useEffect(() => {
    if (isOpen) {
      window.addEventListener('keydown', handleKeyDown);
    }

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, handleKeyDown]);

  if (!isOpen) {
    return null;
  }

  const headerId = `${modalId}-header`;

  return ReactDOM.createPortal(
    <ModalContext.Provider value={{ headerId }}>
      <FocusLock disabled={false}>
        <div className={MODAL_CONTAINER_CLASS}>
          <div
            ref={backgroundRef}
            className={backdropClassName}
            onMouseDown={handleBackdropBeginPress}
            onMouseUp={handleBackdropEndPress}
          >
            <div
              className={classNames(className, MODAL_SIZE_CLASSES[size])}
              style={style}
              role="dialog"
              aria-modal="true"
              aria-labelledby={headerId}
            >
              <ErrorBoundary
                errorComponent={ModalError}
                onModalClose={onModalClose}
              >
                {children}
              </ErrorBoundary>
            </div>
          </div>
        </div>
      </FocusLock>
    </ModalContext.Provider>,
    node!
  );
}

export default Modal;
