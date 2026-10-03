import classNames from 'classnames';
import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import TokenCase from './TokenCase';
import TokenSeparator from './TokenSeparator';

const OPTION_CLASS =
  'group m-[3px] flex flex-wrap items-stretch border border-[var(--borderColor)]';

const TOKEN_CASE_CLASS: Record<TokenCase, string> = {
  lower: 'lowercase',
  title: '',
  upper: 'uppercase',
};

const TOKEN_BASE_CLASS =
  'bg-[var(--popoverTitleBackgroundColor)] p-1.5 font-[Ubuntu_Mono,Menlo,Monaco,Consolas,Courier_New,monospace] group-hover:bg-[#ddd]';

const EXAMPLE_BASE_CLASS =
  'flex items-center justify-between bg-[var(--popoverBodyBackgroundColor)] p-1.5 group-hover:bg-[#ccc]';

const FLEX_FULL_CLASS = 'grow shrink-0 basis-auto';
const FLEX_HALF_CLASS =
  'grow shrink-0 basis-auto min-[480px]:grow-0 min-[480px]:basis-1/2';

interface NamingOptionProps {
  token: string;
  tokenSeparator: TokenSeparator;
  example: string;
  tokenCase: TokenCase;
  isFullFilename?: boolean;
  footNotes?: string;
  size?: 'small' | 'large';
  onPress: ({
    isFullFilename,
    tokenValue,
  }: {
    isFullFilename: boolean;
    tokenValue: string;
  }) => void;
}

function NamingOption(props: NamingOptionProps) {
  const {
    token,
    tokenSeparator,
    example,
    tokenCase,
    isFullFilename = false,
    footNotes,
    size = 'small',
    onPress,
  } = props;

  const handlePress = useCallback(() => {
    let tokenValue = token;

    tokenValue = tokenValue.replace(/ /g, tokenSeparator);

    if (tokenCase === 'lower') {
      tokenValue = token.toLowerCase();
    } else if (tokenCase === 'upper') {
      tokenValue = token.toUpperCase();
    }

    onPress({ isFullFilename, tokenValue });
  }, [token, tokenCase, tokenSeparator, isFullFilename, onPress]);

  return (
    <Link
      className={classNames(
        OPTION_CLASS,
        size === 'small' ? 'w-full md:w-[490px]' : 'w-full',
        TOKEN_CASE_CLASS[tokenCase]
      )}
      onPress={handlePress}
    >
      <div
        className={classNames(
          TOKEN_BASE_CLASS,
          isFullFilename ? FLEX_FULL_CLASS : FLEX_HALF_CLASS
        )}
      >
        {token.replace(/ /g, tokenSeparator)}
      </div>

      <div
        className={classNames(
          EXAMPLE_BASE_CLASS,
          isFullFilename ? FLEX_FULL_CLASS : FLEX_HALF_CLASS
        )}
      >
        {example.replace(/ /g, tokenSeparator)}

        {footNotes ? (
          <div className="p-[2px] text-[#aaa]">
            <sup>{footNotes}</sup>
          </div>
        ) : null}
      </div>
    </Link>
  );
}

export default NamingOption;
