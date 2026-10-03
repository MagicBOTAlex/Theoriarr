import React from 'react';

export interface SummaryItem {
  label: string;
  value: string;
  secondary?: string;
}

interface StatisticsSummaryProps {
  items: SummaryItem[];
}

function StatisticsSummary({ items }: StatisticsSummaryProps) {
  return (
    <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-[10px]">
      {items.map((item) => {
        return (
          <div
            key={item.label}
            className="rounded-[3px] bg-[var(--cardBackgroundColor)] p-[15px] text-center shadow-[0_0_10px_1px_var(--cardShadowColor)]"
          >
            <div className="text-[24px] font-light text-[var(--textColor)]">
              {item.value}
            </div>
            <div className="text-[13px] text-[var(--helpTextColor)]">
              {item.label}
            </div>

            {item.secondary ? (
              <div className="mt-[2px] text-[12px] text-[var(--dimColor)]">
                {item.secondary}
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}

export default StatisticsSummary;
