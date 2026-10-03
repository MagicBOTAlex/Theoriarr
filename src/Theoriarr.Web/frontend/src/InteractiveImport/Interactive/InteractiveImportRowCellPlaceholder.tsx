import classNames from 'classnames';
import React from 'react';

interface InteractiveImportRowCellPlaceholderProps {
  isOptional?: boolean;
}

function InteractiveImportRowCellPlaceholder(
  props: InteractiveImportRowCellPlaceholderProps
) {
  return (
    <span
      className={classNames(
        'inline-block -my-2 h-[25px] w-full border-2 border-dashed border-[var(--dangerColor)]',
        props.isOptional && 'border-2 border-dashed border-[var(--gray)]'
      )}
    />
  );
}

export default InteractiveImportRowCellPlaceholder;
