import React from 'react';

interface ChartContainerProps {
  title: string;
  children: React.ReactNode;
}

function ChartContainer({ title, children }: ChartContainerProps) {
  return (
    <div className="min-w-0 grow shrink basis-[360px] rounded-[3px] bg-[var(--cardBackgroundColor)] p-[15px] shadow-[0_0_10px_1px_var(--cardShadowColor)]">
      <div className="mb-[10px] text-[18px] font-light text-[var(--textColor)]">
        {title}
      </div>

      <div className="relative h-[300px]">{children}</div>
    </div>
  );
}

export default ChartContainer;
