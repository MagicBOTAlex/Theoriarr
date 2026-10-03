import React from 'react';

interface PageToolbarProps {
  className?: string;
  children: React.ReactNode;
}

function PageToolbar({
  className = 'flex h-15 flex-none justify-between bg-[var(--toolbarBackgroundColor)] px-5 text-[var(--toolbarColor)] leading-[60px] max-md:px-2.5',
  children,
}: PageToolbarProps) {
  return <div className={className}>{children}</div>;
}

export default PageToolbar;
