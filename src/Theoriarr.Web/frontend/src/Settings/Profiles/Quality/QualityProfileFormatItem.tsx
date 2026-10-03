import classNames from 'classnames';
import React, { useCallback } from 'react';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import NumberInput from 'Components/Form/NumberInput';
import { InputChanged } from 'typings/inputs';

const SCORE_INPUT_CLASS = classNames(
  INPUT_CLASS,
  'w-[100px] h-[30px]! border-none! rounded-none! bg-transparent!'
);

interface QualityProfileFormatItemProps {
  formatId: number;
  name: string;
  score?: number;
  onScoreChange: (formatId: number, score: number) => void;
}

function QualityProfileFormatItem({
  formatId,
  name,
  score = 0,
  onScoreChange,
}: QualityProfileFormatItemProps) {
  const handleScoreChange = useCallback(
    ({ value }: InputChanged<number>) => {
      onScoreChange(formatId, value);
    },
    [formatId, onScoreChange]
  );

  return (
    <div className="flex py-[4px] px-0 w-full">
      <div className="flex items-stretch w-full border border-solid border-[#aaa] rounded-[4px] bg-[var(--inputBackgroundColor)]">
        <label className="flex grow mb-0 ml-[14px] w-full font-normal leading-[30px] cursor-text">
          <div className="flex grow">{name}</div>
          <NumberInput
            containerClassName="flex grow-0"
            className={SCORE_INPUT_CLASS}
            name={name}
            value={score}
            // @ts-expect-error - mismatched types
            onChange={handleScoreChange}
          />
        </label>
      </div>
    </div>
  );
}

export default QualityProfileFormatItem;
