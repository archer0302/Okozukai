<script setup lang="ts">
import { computed } from 'vue';
import { Doughnut } from 'vue-chartjs';
import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend,
} from 'chart.js';
import type { TransactionSpendingByTagResponse, TagResponse } from '../../types/transaction';

ChartJS.register(ArcElement, Tooltip, Legend);

const props = defineProps<{
  spending: TransactionSpendingByTagResponse | null;
  currency: string;
  isDark: boolean;
  tags?: TagResponse[];
}>();

const fallbackColors = [
  '#6366f1', '#f43f5e', '#10b981', '#f59e0b', '#8b5cf6',
  '#06b6d4', '#ec4899', '#84cc16', '#ef4444', '#14b8a6',
];

const getTagColor = (tagId: string | null, index: number): string => {
  if (tagId && props.tags) {
    const tag = props.tags.find(t => t.id === tagId);
    if (tag) return tag.color;
  }
  return fallbackColors[index % fallbackColors.length];
};

const chartData = computed(() => {
  const items = props.spending?.items ?? [];
  if (items.length === 0) return null;

  return {
    labels: items.map(i => i.tagName),
    datasets: [
      {
        data: items.map(i => i.totalOut),
        backgroundColor: items.map((item, idx) => getTagColor(item.tagId, idx)),
        borderColor: props.isDark ? '#1f2937' : '#ffffff',
        borderWidth: 2,
        hoverOffset: 6,
      },
    ],
  };
});

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: '55%',
  plugins: {
    legend: {
      position: 'right' as const,
      labels: {
        color: props.isDark ? '#d1d5db' : '#374151',
        usePointStyle: true,
        pointStyle: 'circle',
        padding: 12,
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
          const total = ctx.dataset.data.reduce((sum: number, v: number) => sum + v, 0);
          const pct = total > 0 ? ((ctx.raw / total) * 100).toFixed(1) : '0';
          return `${ctx.label}: ${value} (${pct}%)`;
        },
      },
    },
  },
}));
</script>

<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 p-5">
    <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-4">Spending by Tag</h3>
    <div v-if="!chartData" class="text-sm text-gray-500 dark:text-gray-400 text-center py-8">
      No spending data available for chart.
    </div>
    <div v-else class="h-64">
      <Doughnut :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
