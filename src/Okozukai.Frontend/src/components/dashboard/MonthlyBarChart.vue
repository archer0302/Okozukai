<script setup lang="ts">
import { computed } from 'vue';
import { Bar } from 'vue-chartjs';
import {
  Chart as ChartJS,
  BarElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
} from 'chart.js';
import type { TransactionYearGroupResponse } from '../../types/transaction';

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend);

const props = defineProps<{
  grouped: TransactionYearGroupResponse[];
  currency: string;
  isDark: boolean;
}>();

const chartData = computed(() => {
  const months: { label: string; totalIn: number; totalOut: number }[] = [];

  // Flatten year/month groups into a chronological list
  const sorted = [...props.grouped].sort((a, b) => a.year - b.year);
  for (const yearGroup of sorted) {
    const sortedMonths = [...yearGroup.months].sort((a, b) => a.month - b.month);
    for (const monthGroup of sortedMonths) {
      const label = new Date(yearGroup.year, monthGroup.month - 1, 1)
        .toLocaleString(undefined, { month: 'short', year: '2-digit' });
      const rollup = monthGroup.rollups[0];
      months.push({
        label,
        totalIn: rollup?.totalIn ?? 0,
        totalOut: rollup?.totalOut ?? 0,
      });
    }
  }

  return {
    labels: months.map(m => m.label),
    datasets: [
      {
        label: 'Income',
        data: months.map(m => m.totalIn),
        backgroundColor: 'rgba(34, 197, 94, 0.7)',
        borderColor: 'rgb(34, 197, 94)',
        borderWidth: 1,
        borderRadius: 4,
      },
      {
        label: 'Expenses',
        data: months.map(m => m.totalOut),
        backgroundColor: 'rgba(239, 68, 68, 0.7)',
        borderColor: 'rgb(239, 68, 68)',
        borderWidth: 1,
        borderRadius: 4,
      },
    ],
  };
});

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'top' as const,
      labels: {
        color: props.isDark ? '#d1d5db' : '#374151',
        usePointStyle: true,
        pointStyle: 'rectRounded',
      },
    },
    tooltip: {
      callbacks: {
        label: (ctx: any) => {
          const value = new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: props.currency,
          }).format(ctx.raw);
          return `${ctx.dataset.label}: ${value}`;
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
      beginAtZero: true,
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
    <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-4">Monthly Income vs Expenses</h3>
    <div v-if="grouped.length === 0" class="text-sm text-gray-500 dark:text-gray-400 text-center py-8">
      No transaction data available for chart.
    </div>
    <div v-else class="h-64">
      <Bar :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
