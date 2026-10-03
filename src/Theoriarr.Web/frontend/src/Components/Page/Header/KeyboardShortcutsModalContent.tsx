import React from 'react';
import { Shortcut, shortcuts } from 'Components/keyboardShortcuts';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { useSystemStatusData } from 'System/Status/useSystemStatus';
import translate from 'Utilities/String/translate';

const SHORTCUT_CLASS = 'flex justify-between px-5 py-[5px] text-[18px]';

const KEY_CLASS =
  'rounded-[3px] bg-[var(--defaultColor)] px-1 py-0.5 text-base text-[var(--white)] shadow-[inset_0_-1px_0_rgba(0,0,0,0.25)]';

function getShortcuts() {
  const allShortcuts: Shortcut[] = [];

  Object.keys(shortcuts).forEach((key) => {
    allShortcuts.push(shortcuts[key]);
  });

  return allShortcuts;
}

function getShortcutKey(combo: string, isOsx: boolean) {
  const comboMatch = combo.match(/(.+?)\+(.)/);

  if (!comboMatch) {
    return combo;
  }

  const modifier = comboMatch[1];
  const key = comboMatch[2];
  let osModifier = modifier;

  if (modifier === 'mod') {
    osModifier = isOsx ? 'cmd' : 'ctrl';
  }

  return `${osModifier} + ${key}`;
}

interface KeyboardShortcutsModalContentProps {
  onModalClose: () => void;
}

function KeyboardShortcutsModalContent({
  onModalClose,
}: KeyboardShortcutsModalContentProps) {
  const { isOsx } = useSystemStatusData();
  const allShortcuts = getShortcuts();

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('KeyboardShortcuts')}</ModalHeader>

      <ModalBody>
        {allShortcuts.map((shortcut) => {
          return (
            <div key={shortcut.name} className={SHORTCUT_CLASS}>
              <div className={KEY_CLASS}>
                {getShortcutKey(shortcut.key, isOsx)}
              </div>

              <div>{shortcut.name}</div>
            </div>
          );
        })}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default KeyboardShortcutsModalContent;
