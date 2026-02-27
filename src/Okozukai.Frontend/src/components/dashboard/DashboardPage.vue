<script setup lang="ts">
import { ref, onMounted, computed, watch, inject, type Ref } from 'vue';
import type { PropType } from 'vue';
import { transactionService } from '../../api/transactionService';
import type {
  JournalResponse,
  TransactionYearGroupResponse,
  TransactionSpendingByTagResponse,
  TransactionSummaryResponse,
  SpendingByTagMonthlyResponse,
  TagResponse,
} from '../../types/transaction';
import MonthlyBarChart from './MonthlyBarChart.vue';
import SpendingPieChart from './SpendingPieChart.vue';
import NetBalanceTrendChart from './NetBalanceTrendChart.vue';
import MonthlyTagStackedBarChart from './MonthlyTagStackedBarChart.vue';

const props = defineProps({
  journal: {
    type: Object as PropType<JournalResponse>,
    required: true,
  },
});

const loading = ref(true);
const error = ref<string | null>(null);
const summary = ref<TransactionSummaryResponse | null>(null);
const grouped = ref<TransactionYearGroupResponse[]>([]);
const spending = ref<TransactionSpendingByTagResponse | null>(null);
const spendingMonthly = ref<SpendingByTagMonthlyResponse | null>(null);
const tags = ref<TagResponse[]>([]);
const activePreset = ref('all');
const showCustomize = ref(false);

type ChartId = 'monthlyBar' | 'spendingPie' | 'balanceTrend' | 'monthlyTagStacked';
interface ChartConfig { id: ChartId; label: string }

const chartConfigs: ChartConfig[] = [
  { id: 'monthlyBar', label: 'Monthly Income vs Expenses' },
  { id: 'spendingPie', label: 'Spending by Tag' },
  { id: 'balanceTrend', label: 'Net Balance Trend' },
  { id: 'monthlyTagStacked', label: 'Monthly Spending by Tag' },
];

const storageKey = computed(() => `dashboard-widgets-${props.journal.id}`);

const loadVisibility = (): Record<ChartId, boolean> => {
  try {
    const saved = localStorage.getItem(storageKey.value);
    if (saved) return JSON.parse(saved);
  } catch { /* ignore */ }
  return { monthlyBar: true, spendingPie: true, balanceTrend: true, monthlyTagStacked: true };
};

const visibleCharts = ref<Record<ChartId, boolean>>(loadVisibility());

watch(visibleCharts, (val) => {
  localStorage.setItem(storageKey.value, JSON.stringify(val));
}, { deep: true });

const toggleChart = (id: ChartId) => {
  visibleCharts.value = { ...visibleCharts.value, [id]: !visibleCharts.value[id] };
};

const anyChartVisible = computed(() => Object.values(visibleCharts.value).some(Boolean));

// Inject reactive isDark from App.vue shell; fallback to false for unit tests
const isDark = inject<Ref<boolean>>('isDark', ref(false));

type DatePreset = { label: string; key: string; from?: string };
const presets: DatePreset[] = [
  { label: 'This Month', key: 'month' },
  { label: 'Last 3 Months', key: '3m' },
  { label: 'Last 6 Months', key: '6m' },
  { label: 'This Year', key: 'year' },
  { label: 'All Time', key: 'all' },
];

const getPresetFrom = (key: string): string | undefined => {
  const now = new Date();
  switch (key) {
    case 'month':
      return new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
    case '3m':
      return new Date(now.getFullYear(), now.getMonth() - 2, 1).toISOString();
    case '6m':
      return new Date(now.getFullYear(), now.getMonth() - 5, 1).toISOString();
    case 'year':
      return new Date(now.getFullYear(), 0, 1).toISOString();
    default:
      return undefined;
  }
};

const fetchData = async () => {
  loading.value = true;
  error.value = null;
  try {
    const from = getPresetFrom(activePreset.value);
    const query = { journalId: props.journal.id, from };
    const [s, g, sp, spm, t] = await Promise.all([
      transactionService.getSummary(query),
      transactionService.getGrouped(query),
      transactionService.getSpendingByTag(query),
      transactionService.getSpendingByTagMonthly(query),
      transactionService.getTags(),
    ]);
    summary.value = s;
    grouped.value = g;
    spending.value = sp;
    spendingMonthly.value = spm;
    tags.value = t;
  } catch (err) {
    console.error('Failed to load dashboard data:', err);
    error.value = 'Failed to load dashboard data. Is the API running?';
  } finally {
    loading.value = false;
  }
};

const selectPreset = (key: string) => {
  activePreset.value = key;
  void fetchData();
};

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);

onMounted(fetchData);
</script>

<template>
  <div class="space-y-6">
    <!-- Summary KPI cards -->
    <div v-if="summary" class="bg-white dark:bg-gray-800 px-5 py-4 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 grid grid-cols-3 gap-4">
      <div>
        <p class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">{{ journal.primaryCurrency }} Balance</p>
        <p class="text-xl font-bold mt-0.5" :class="summary.net >= 0 ? 'text-indigo-600 dark:text-indigo-400' : 'text-red-600 dark:text-red-400'">
          {{ formatCurrency(summary.net, journal.primaryCurrency) }}
        </p>
      </div>
      <div>
        <p class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Total In</p>
        <p class="text-xl font-bold mt-0.5 text-green-600 dark:text-green-400">{{ formatCurrency(summary.totalIn, journal.primaryCurrency) }}</p>
      </div>
      <div>
        <p class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Total Out</p>
        <p class="text-xl font-bold mt-0.5 text-red-600 dark:text-red-400">{{ formatCurrency(summary.totalOut, journal.primaryCurrency) }}</p>
      </div>
    </div>

    <!-- Date presets + Customize button -->
    <div class="flex items-center justify-between flex-wrap gap-2">
      <div class="flex gap-2 flex-wrap">
        <button
          v-for="preset in presets"
          :key="preset.key"
          class="px-3 py-1.5 rounded-lg text-sm font-medium transition-colors"
          :class="activePreset === preset.key
            ? 'bg-indigo-600 text-white shadow-sm'
            : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700'"
          @click="selectPreset(preset.key)"
        >
          {{ preset.label }}
        </button>
      </div>
      <button
        class="px-3 py-1.5 rounded-lg text-sm font-medium transition-colors bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700"
        @click="showCustomize = !showCustomize"
      >
        ⚙ Customize
      </button>
    </div>

    <!-- Customize panel -->
    <div
      v-if="showCustomize"
      class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 p-4"
    >
      <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Toggle Charts</h3>
      <div class="flex flex-wrap gap-3">
        <label
          v-for="chart in chartConfigs"
          :key="chart.id"
          class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg border cursor-pointer select-none text-sm transition-colors"
          :class="visibleCharts[chart.id]
            ? 'bg-indigo-50 dark:bg-indigo-900/30 border-indigo-300 dark:border-indigo-700 text-indigo-700 dark:text-indigo-300'
            : 'border-gray-200 dark:border-gray-700 text-gray-500 dark:text-gray-500'"
        >
          <input
            type="checkbox"
            :checked="visibleCharts[chart.id]"
            class="sr-only"
            @change="toggleChart(chart.id)"
          />
          <span class="w-3 h-3 rounded-sm border flex items-center justify-center text-xs"
            :class="visibleCharts[chart.id]
              ? 'bg-indigo-600 border-indigo-600 text-white'
              : 'border-gray-300 dark:border-gray-600'"
          >
            <span v-if="visibleCharts[chart.id]">✓</span>
          </span>
          {{ chart.label }}
        </label>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="text-center py-12">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-4 border-indigo-500 border-t-transparent"></div>
      <p class="mt-2 text-gray-500 dark:text-gray-400">Loading dashboard...</p>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 p-4 rounded-lg">
      {{ error }}
    </div>

    <!-- Charts -->
    <div v-else-if="anyChartVisible" class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div v-if="visibleCharts.monthlyBar" class="lg:col-span-2">
        <MonthlyBarChart :grouped="grouped" :currency="journal.primaryCurrency" :isDark="isDark" />
      </div>
      <SpendingPieChart v-if="visibleCharts.spendingPie" :spending="spending" :currency="journal.primaryCurrency" :isDark="isDark" :tags="tags" />
      <NetBalanceTrendChart v-if="visibleCharts.balanceTrend" :grouped="grouped" :currency="journal.primaryCurrency" :isDark="isDark" />
      <div v-if="visibleCharts.monthlyTagStacked" class="lg:col-span-2">
        <MonthlyTagStackedBarChart :data="spendingMonthly" :currency="journal.primaryCurrency" :isDark="isDark" :tags="tags" />
      </div>
    </div>

    <!-- Empty state when all charts hidden -->
    <div v-else class="bg-white dark:bg-gray-800 p-12 rounded-xl border border-dashed border-gray-300 dark:border-gray-600 text-center">
      <p class="text-gray-500 dark:text-gray-400">All charts are hidden. Click <strong>Customize</strong> to toggle them back on.</p>
    </div>
  </div>
</template>
