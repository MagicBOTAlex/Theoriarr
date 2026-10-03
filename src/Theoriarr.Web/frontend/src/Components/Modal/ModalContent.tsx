import React from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

export const MODAL_CONTENT_CLASS =
  'relative flex w-full min-h-0 grow flex-col overflow-hidden bg-[var(--modalBackgroundColor)]';

const CLOSE_BUTTON_CLASS =
  'absolute top-2 right-2 z-[1] flex h-9 w-9 items-center justify-center rounded-lg text-[color-mix(in_srgb,var(--textColor),transparent_25%)] transition-colors hover:bg-[color-mix(in_srgb,var(--textColor),transparent_90%)] hover:text-[var(--modalCloseButtonHoverColor)]';

interface ModalContentProps extends React.HTMLAttributes<HTMLDivElement> {
  className?: string;
  children?: React.ReactNode;
  showCloseButton?: boolean;
  onModalClose: () => void;
}

function ModalContent({
  className = MODAL_CONTENT_CLASS,
  children,
  showCloseButton = true,
  onModalClose,
  ...otherProps
}: ModalContentProps) {
  return (
    <div className={className} {...otherProps}>
      {showCloseButton && (
        <Link className={CLOSE_BUTTON_CLASS} onPress={onModalClose}>
          <Icon name={icons.CLOSE} size={18} title={translate('Close')} />
        </Link>
      )}

      {children}
    </div>
  );
}

export default ModalContent;
