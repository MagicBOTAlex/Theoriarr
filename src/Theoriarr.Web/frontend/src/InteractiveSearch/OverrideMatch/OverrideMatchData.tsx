import classNames from 'classnames';
import React from 'react';
import Link from 'Components/Link/Link';

const PLACEHOLDER_CLASS =
  'inline-block my-[-2px] w-full [outline:2px_dashed_var(--dangerColor)] [outline-offset:-2px]';
const OPTIONAL_CLASS = '[outline:2px_dashed_var(--gray)]';

interface OverrideMatchDataProps {
  value?: string | number | JSX.Element | JSX.Element[];
  isDisabled?: boolean;
  isOptional?: boolean;
  onPress: () => void;
}

function OverrideMatchData(props: OverrideMatchDataProps) {
  const { value, isDisabled = false, isOptional, onPress } = props;

  return (
    <Link className="w-full" isDisabled={isDisabled} onPress={onPress}>
      {(value == null || (Array.isArray(value) && value.length === 0)) &&
      !isDisabled ? (
        <span
          className={classNames(
            PLACEHOLDER_CLASS,
            isOptional && OPTIONAL_CLASS
          )}
        >
          &nbsp;
        </span>
      ) : (
        value
      )}
    </Link>
  );
}

export default OverrideMatchData;
