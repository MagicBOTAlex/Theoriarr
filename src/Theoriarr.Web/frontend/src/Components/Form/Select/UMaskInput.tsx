/* eslint-disable no-bitwise */
import React, { SyntheticEvent } from 'react';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import { INPUT_UNIT_CLASS } from '../InputClassNames';
import EnhancedSelectInput, {
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';

const MONO_FONT_CLASS =
  '[font-family:"Ubuntu_Mono","Menlo","Monaco","Consolas","Courier_New",monospace]';

const INPUT_UNIT_UMASK_CLASS = `${INPUT_UNIT_CLASS} right-[40px] ${MONO_FONT_CLASS}`;
const UNIT_DETAILS_CLASS = `${MONO_FONT_CLASS} w-[90px] text-right`;
const VALUE_CLASS = 'w-[50px] text-right';
const DETAILS_CLASS =
  'mt-[5px] ml-[17px] leading-[20px] [&>div]:flex [&>div>label]:flex-[0_0_50px]';

const umaskOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: '755',
    get value() {
      return translate('Umask755Description', { octal: '755' });
    },
    hint: 'drwxr-xr-x',
  },
  {
    key: '775',
    get value() {
      return translate('Umask775Description', { octal: '775' });
    },
    hint: 'drwxrwxr-x',
  },
  {
    key: '770',
    get value() {
      return translate('Umask770Description', { octal: '770' });
    },
    hint: 'drwxrwx---',
  },
  {
    key: '750',
    get value() {
      return translate('Umask750Description', { octal: '750' });
    },
    hint: 'drwxr-x---',
  },
  {
    key: '777',
    get value() {
      return translate('Umask777Description', { octal: '777' });
    },
    hint: 'drwxrwxrwx',
  },
];

function formatPermissions(permissions: number) {
  const hasSticky = permissions & 0o1000;
  const hasSetGID = permissions & 0o2000;
  const hasSetUID = permissions & 0o4000;

  let result = '';

  for (let i = 0; i < 9; i++) {
    const bit = (permissions & (1 << i)) !== 0;
    let digit = bit ? 'xwr'[i % 3] : '-';

    if (i === 6 && hasSetUID) {
      digit = bit ? 's' : 'S';
    } else if (i === 3 && hasSetGID) {
      digit = bit ? 's' : 'S';
    } else if (i === 0 && hasSticky) {
      digit = bit ? 't' : 'T';
    }

    result = digit + result;
  }

  return result;
}

export interface UMaskInputProps {
  name: string;
  value: string;
  hasError?: boolean;
  hasWarning?: boolean;
  onChange: (change: InputChanged) => void;
  onFocus?: (event: SyntheticEvent) => void;
  onBlur?: (event: SyntheticEvent) => void;
}

function UMaskInput({ name, value, onChange }: UMaskInputProps) {
  const valueNum = parseInt(value, 8);
  const umaskNum = 0o777 & ~valueNum;
  const umask = umaskNum.toString(8).padStart(4, '0');
  const folderNum = 0o777 & ~umaskNum;
  const folder = folderNum.toString(8).padStart(3, '0');
  const fileNum = 0o666 & ~umaskNum;
  const file = fileNum.toString(8).padStart(3, '0');
  const unit = formatPermissions(folderNum);

  const values = umaskOptions.map((v) => {
    return { ...v, hint: <span className={MONO_FONT_CLASS}>{v.hint}</span> };
  });

  return (
    <div>
      <div className="flex">
        <div className="relative w-full">
          <EnhancedSelectInput
            name={name}
            value={value}
            values={values}
            isEditable={true}
            onChange={onChange}
          />

          <div className={INPUT_UNIT_UMASK_CLASS}>d{unit}</div>
        </div>
      </div>

      <div className={DETAILS_CLASS}>
        <div>
          <label>{translate('Umask')}</label>
          <div className={VALUE_CLASS}>{umask}</div>
        </div>

        <div>
          <label>{translate('Folder')}</label>
          <div className={VALUE_CLASS}>{folder}</div>
          <div className={UNIT_DETAILS_CLASS}>
            d{formatPermissions(folderNum)}
          </div>
        </div>

        <div>
          <label>{translate('File')}</label>
          <div className={VALUE_CLASS}>{file}</div>
          <div className={UNIT_DETAILS_CLASS}>{formatPermissions(fileNum)}</div>
        </div>
      </div>
    </div>
  );
}

export default UMaskInput;
