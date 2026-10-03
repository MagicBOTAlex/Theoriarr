import React from 'react';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';

interface UpdateChangesProps {
  title: string;
  changes: string[];
}

function UpdateChanges(props: UpdateChangesProps) {
  const { title, changes } = props;

  if (changes.length === 0) {
    return null;
  }

  return (
    <div>
      <div className="mt-[10px] text-[16px]">{title}</div>
      <ul>
        {changes.map((change, index) => {
          return (
            <li key={index}>
              <InlineMarkdown data={change} />
            </li>
          );
        })}
      </ul>
    </div>
  );
}

export default UpdateChanges;
