import React, { useCallback, useEffect, useState } from 'react';
import { toggleIsSidebarVisible } from 'App/appStore';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import useKeyboardShortcuts from 'Helpers/Hooks/useKeyboardShortcuts';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import KeyboardShortcutsModal from './KeyboardShortcutsModal';
import PageHeaderActionsMenu from './PageHeaderActionsMenu';

function PageHeader() {
  const [isKeyboardShortcutsModalOpen, setIsKeyboardShortcutsModalOpen] =
    useState(false);

  const { bindShortcut, unbindShortcut } = useKeyboardShortcuts();

  const handleSidebarToggle = useCallback(() => {
    toggleIsSidebarVisible();
  }, []);

  const handleOpenKeyboardShortcutsModal = useCallback(() => {
    setIsKeyboardShortcutsModalOpen(true);
  }, []);

  const handleKeyboardShortcutsModalClose = useCallback(() => {
    setIsKeyboardShortcutsModalOpen(false);
  }, []);

  useEffect(() => {
    bindShortcut(
      'openKeyboardShortcutsModal',
      handleOpenKeyboardShortcutsModal
    );

    return () => {
      unbindShortcut('openKeyboardShortcutsModal');
    };
  }, [handleOpenKeyboardShortcutsModal, bindShortcut, unbindShortcut]);

  return (
    <div className="flex h-15 flex-none items-center border-b border-[color-mix(in_srgb,var(--color-neutral-content),transparent_90%)] bg-neutral text-neutral-content">
      <div className="flex flex-none basis-[210px] items-center gap-3 pl-5 max-md:basis-[60px]">
        <Link className="leading-none" to="/">
          <img
            className="h-8 w-8 brightness-0 invert"
            src={`${window.Theoriarr.services.series.urlBase}/Content/Images/logo.svg`}
            alt="Theoriarr Logo"
          />
        </Link>

        <span className="truncate text-[15px] font-semibold tracking-wide max-md:hidden">
          Theoriarr
        </span>
      </div>

      <div className="hidden flex-none basis-[45px] justify-center mr-3.5 max-md:flex">
        <IconButton
          id="sidebar-toggle-button"
          name={icons.NAVBAR_COLLAPSE}
          aria-label={translate('Menu')}
          onPress={handleSidebarToggle}
        />
      </div>

      <div className="flex flex-grow justify-end">
        <PageHeaderActionsMenu
          onKeyboardShortcutsPress={handleOpenKeyboardShortcutsModal}
        />
      </div>

      <KeyboardShortcutsModal
        isOpen={isKeyboardShortcutsModalOpen}
        onModalClose={handleKeyboardShortcutsModalClose}
      />
    </div>
  );
}

export default PageHeader;
