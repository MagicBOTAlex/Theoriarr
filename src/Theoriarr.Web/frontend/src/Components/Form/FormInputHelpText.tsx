import classNames from 'classnames';
import React from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';

const HELP_TEXT_CLASS = 'mt-[5px] text-[var(--helpTextColor)] leading-[20px]';

interface FormInputHelpTextProps {
  className?: string;
  text: string;
  link?: string;
  tooltip?: string;
  isError?: boolean;
  isWarning?: boolean;
  isCheckInput?: boolean;
}

function FormInputHelpText({
  className,
  text,
  link,
  tooltip,
  isError = false,
  isWarning = false,
  isCheckInput = false,
}: FormInputHelpTextProps) {
  return (
    <div
      className={classNames(
        HELP_TEXT_CLASS,
        className,
        isError && 'text-[var(--dangerColor)]!',
        isWarning && 'text-[var(--warningColor)]!',
        isCheckInput && 'pl-[30px]'
      )}
    >
      {text}

      {link ? (
        <Link
          className={classNames(
            'ml-[5px]',
            isError && 'text-[var(--dangerColor)]! hover:text-[#e01313]!',
            isWarning && 'text-[var(--warningColor)]! hover:text-[#e36c00]!'
          )}
          to={link}
          title={tooltip}
        >
          <Icon name={icons.EXTERNAL_LINK} />
        </Link>
      ) : null}

      {!link && tooltip ? (
        <Icon containerClassName="ml-[5px]" name={icons.INFO} title={tooltip} />
      ) : null}
    </div>
  );
}

export default FormInputHelpText;
