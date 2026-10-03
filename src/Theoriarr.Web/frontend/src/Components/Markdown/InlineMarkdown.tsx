import React, { ReactElement } from 'react';
import Link from 'Components/Link/Link';

interface InlineMarkdownProps {
  className?: string;
  data?: string;
  blockClassName?: string;
}

function InlineMarkdown(props: InlineMarkdownProps) {
  const { className, data, blockClassName } = props;

  const markdownBlocks: (ReactElement | string)[] = [];

  if (data) {
    // Match links and inline code in a single pass so a string containing both
    // doesn't get its content emitted twice.
    const tokenRegex = RegExp(/\[(.+?)\]\((.+?)\)|`([^`]+)`/g);

    let endIndex = 0;
    let key = 0;
    let match = null;

    while ((match = tokenRegex.exec(data)) !== null) {
      if (match.index > endIndex) {
        markdownBlocks.push(data.substring(endIndex, match.index));
      }

      if (match[1] !== undefined && match[2] !== undefined) {
        markdownBlocks.push(
          <Link key={`link-${key}`} to={match[2]}>
            {match[1]}
          </Link>
        );
      } else if (match[3] !== undefined) {
        markdownBlocks.push(
          <code key={`code-${key}`} className={blockClassName ?? undefined}>
            {match[3]}
          </code>
        );
      }

      key += 1;
      endIndex = match.index + match[0].length;
    }

    if (endIndex < data.length) {
      markdownBlocks.push(data.substring(endIndex, data.length));
    }
  }

  return <span className={className}>{markdownBlocks}</span>;
}

export default InlineMarkdown;
