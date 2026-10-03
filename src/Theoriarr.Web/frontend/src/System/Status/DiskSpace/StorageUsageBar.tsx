import {
  BarElement,
  CategoryScale,
  Chart,
  ChartOptions,
  LinearScale,
  Tooltip,
  TooltipItem,
} from 'chart.js';
import React, { useMemo } from 'react';
import { Bar } from 'react-chartjs-2';
import useChartColors from 'Statistics/useChartColors';
import formatBytes from 'Utilities/Number/formatBytes';

Chart.register(BarElement, CategoryScale, LinearScale, Tooltip);

export interface StorageUsageBarItem {
  label: string;
  value: number;
}

interface StorageUsageBarProps {
  items: StorageUsageBarItem[];
}

const MAX_LABEL_LENGTH = 32;
const BAR_HEIGHT = 26;
const MIN_HEIGHT = 150;

function formatLabel(label: string) {
  return label.length > MAX_LABEL_LENGTH
    ? `${label.slice(0, MAX_LABEL_LENGTH - 3)}...`
    : label;
}

function StorageUsageBar({ items }: StorageUsageBarProps) {
  const colors = useChartColors();

  const data = useMemo(() => {
    return {
      labels: items.map((item) => formatLabel(item.label)),
      datasets: [
        {
          data: items.map((item) => item.value),
          backgroundColor: colors.bar,
          borderRadius: 3,
          maxBarThickness: 18,
        },
      ],
    };
  }, [colors, items]);

  const options = useMemo<ChartOptions<'bar'>>(() => {
    return {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          beginAtZero: true,
          ticks: {
            color: colors.text,
            callback: (value) => formatBytes(Number(value)),
          },
          grid: {
            color: colors.grid,
          },
        },
        y: {
          ticks: {
            color: colors.text,
            autoSkip: false,
          },
          grid: {
            display: false,
          },
        },
      },
      plugins: {
        legend: {
          display: false,
        },
        tooltip: {
          callbacks: {
            label: (context: TooltipItem<'bar'>) =>
              ` ${formatBytes(context.parsed.x ?? 0)}`,
          },
        },
      },
    };
  }, [colors]);

  const height = Math.max(MIN_HEIGHT, items.length * BAR_HEIGHT);

  return (
    <div className="relative w-full" style={{ height }}>
      <Bar data={data} options={options} />
    </div>
  );
}

export default StorageUsageBar;
