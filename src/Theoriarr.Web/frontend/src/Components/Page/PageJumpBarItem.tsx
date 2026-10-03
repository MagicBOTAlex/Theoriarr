import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';

export interface PageJumpBarItemProps {
  label: string;
  onItemPress: (label: string) => void;
}

function PageJumpBarItem({ label, onItemPress }: PageJumpBarItemProps) {
  const handlePress = useCallback(() => {
    onItemPress(label);
  }, [label, onItemPress]);

  return (
    <Link
      className="flex-[1_1_25px] border-b border-[var(--borderColor)] text-center font-bold hover:text-[#777] [&:last-child]:border-none"
      onPress={handlePress}
    >
      {label.toUpperCase()}
    </Link>
  );
}

export default PageJumpBarItem;
