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
import type { SpendingByTagMonthlyResponse, TagResponse } from '../../types/transaction';

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend);

const props = defineProps<{
  data: SpendingByTagMonthlyResponse | null;
  currency: string;
  isDark: boolean;
  tags?: TagResponse[];
}>();

const fallbackColors = [
  '#6366f1', '#f43f5e', '#10b981', '#f59e0b', '#8b5cf6',
  '#06b6d4', '#ec4899', '#84cc16', '#ef4444', '#14b8a6',
];

// Build a stable name→color map: prefer tag.color from tags prop, then fallback palette
const getTagColor = (tagName: string, fallbackIdx: number): string => {
  if (props.tags) {
    const tag = props.tags.find(t => t.name === tagName);
    if (tag) return tag.color;
  }
  return fallbackColors[fallbackIdx % fallbackColors.length];
};

const chartData = computed(() => {
  if (!props.data || props.data.months.length === 0) return null;

  // Collect all unique tag names across months
  const tagNameSet = new Set<string>();
  for (const month of props.data.months) {
    for (const item of month.items) {
      tagNameSet.add(item.tagName);
    }
  }
  const tagNames = [...tagNameSet];

  const labels = props.data.months.map(m =>
    new Date(m.year, m.month - 1, 1).toLocaleString(undefined, { month: 'short', year: '2-digit' })
  );

  const datasets = tagNames.map((name, idx) => ({
    label: name,
    data: props.data!.months.map(m => {
      const item = m.items.find(i => i.tagName === name);
      return item?.totalOut ?? 0;
    }),
    backgroundColor: getTagColor(name, idx) + 'b3',
    borderColor: getTagColor(name, idx),
    borderWidth: 1,
    borderRadius: 2,
  }));

  return { labels, datasets };
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
        font: { size: 11 },
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
      stacked: true,
      ticks: { color: props.isDark ? '#9ca3af' : '#6b7280' },
      grid: { color: props.isDark ? 'rgba(75, 85, 99, 0.3)' : 'rgba(229, 231, 235, 0.5)' },
    },
    y: {
      stacked: true,
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
    <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-4">Monthly Spending by Tag</h3>
    <div v-if="!chartData" class="text-sm text-gray-500 dark:text-gray-400 text-center py-8">
      No spending data available for chart.
    </div>
    <div v-else class="h-64">
      <Bar :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
