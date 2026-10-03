import React from 'react';

interface VirtualTableHeaderProps {
  children?: React.ReactNode;
}

function VirtualTableHeader({ children }: VirtualTableHeaderProps) {
  return <div className="flex">{children}</div>;
}

export default VirtualTableHeader;
