<script setup lang="ts">
import { computed } from 'vue';
import { Line } from 'vue-chartjs';
import {
  Chart as ChartJS,
  LineElement,
  PointElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';
import type { TransactionYearGroupResponse } from '../../types/transaction';

ChartJS.register(LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, Filler);

const props = defineProps<{
  grouped: TransactionYearGroupResponse[];
  currency: string;
  isDark: boolean;
}>();

const chartData = computed(() => {
  const months: { label: string; netChange: number }[] = [];

  const sorted = [...props.grouped].sort((a, b) => a.year - b.year);
  for (const yearGroup of sorted) {
    const sortedMonths = [...yearGroup.months].sort((a, b) => a.month - b.month);
    for (const monthGroup of sortedMonths) {
      const label = new Date(yearGroup.year, monthGroup.month - 1, 1)
        .toLocaleString(undefined, { month: 'short', year: '2-digit' });
      const rollup = monthGroup.rollups[0];
      months.push({ label, netChange: rollup?.netChange ?? 0 });
    }
  }

  // Build cumulative balance
  let cumulative = 0;
  const cumulativeValues = months.map(m => {
    cumulative += m.netChange;
    return cumulative;
  });

  return {
    labels: months.map(m => m.label),
    datasets: [
      {
        label: 'Cumulative Net Balance',
        data: cumulativeValues,
        borderColor: 'rgb(99, 102, 241)',
        backgroundColor: 'rgba(99, 102, 241, 0.1)',
        fill: true,
        tension: 0.3,
        pointRadius: 4,
        pointHoverRadius: 6,
        pointBackgroundColor: 'rgb(99, 102, 241)',
      },
    ],
  };
});

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: false,
    },
    tooltip: {
      callbacks: {
        label: (ctx: any) => {
          const value = new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: props.currency,
          }).format(ctx.raw);
          return `Balance: ${value}`;
        },
      },
    },
  },
  scales: {
    x: {
      ticks: { color: props.isDark ? '#9ca3af' : '#6b7280' },
      grid: { color: props.isDark ? 'rgba(75, 85, 99, 0.3)' : 'rgba(229, 231, 235, 0.5)' },
    },
    y: {
      ticks: {
        color: props.isDark ? '#9ca3af' : '#6b7280',
        callback: (value: any) =>
          new Intl.NumberFormat('en-US', { style: 'currency', currency: props.currency, maximumFractionDigits: 0 }).format(value),
      },
      grid: { color: props.isDark ? 'rgba(75, 85, 99, 0.3)' : 'rgba(229, 231, 235, 0.5)' },
    },
  },
}));
</script>

<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 p-5">
    <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-4">Net Balance Trend</h3>
    <div v-if="grouped.length === 0" class="text-sm text-gray-500 dark:text-gray-400 text-center py-8">
      No transaction data available for chart.
    </div>
    <div v-else class="h-64">
      <Line :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
