import React from 'react';

export const PAGE_CONTENT_FOOTER_CLASS =
  'flex flex-[0_0_auto] p-[20px] bg-[var(--pageFooterBackground)] max-[1450px]:flex-wrap max-[768px]:block';

interface PageContentFooterProps {
  className?: string;
  children: React.ReactNode;
}

function PageContentFooter({
  className = PAGE_CONTENT_FOOTER_CLASS,
  children,
}: PageContentFooterProps) {
  return <div className={className}>{children}</div>;
}

export default PageContentFooter;
