import React from 'react';
import { TagBase } from 'Components/Form/Tag/TagInput';
import TagInputTag, { TagInputTagProps } from 'Components/Form/Tag/TagInputTag';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const TAG_CLASS = 'flex';
const OR_CLASS = 'm-[0_3px] text-[var(--themeDarkColor)] leading-[31px]';

interface FilterBuilderRowValueTagProps extends TagInputTagProps<TagBase> {
  isLastTag: boolean;
}

function FilterBuilderRowValueTag({
  isLastTag,
  ...otherProps
}: FilterBuilderRowValueTagProps) {
  return (
    <div className={TAG_CLASS}>
      <TagInputTag {...otherProps} kind={kinds.DEFAULT} />

      {isLastTag ? null : <div className={OR_CLASS}>{translate('Or')}</div>}
    </div>
  );
}

export default FilterBuilderRowValueTag;
