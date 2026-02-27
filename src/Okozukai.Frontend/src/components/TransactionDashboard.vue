<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue';
import type { PropType } from 'vue';
import { transactionService } from '../api/transactionService';
import {
  TransactionType,
  type TransactionResponse,
  type TransactionSummaryResponse,
  type TransactionYearGroupResponse,
  type TransactionSpendingByTagResponse,
  type TagResponse,
  type JournalResponse
} from '../types/transaction';
import AddTransactionForm from './AddTransactionForm.vue';

const props = defineProps({
  journal: {
    type: Object as PropType<JournalResponse>,
    required: true
  }
});

defineEmits(['journal-updated']);

const summary = ref<TransactionSummaryResponse | null>(null);
const groupedTransactions = ref<TransactionYearGroupResponse[]>([]);
const spendingByTag = ref<TransactionSpendingByTagResponse | null>(null);
const tags = ref<TagResponse[]>([]);
const loading = ref(true);
const error = ref<string | null>(null);
const tagError = ref<string | null>(null);
const showAddForm = ref(false);
const showTagsModal = ref(false);
const editingTransaction = ref<TransactionResponse | null>(null);
const fromDate = ref('');
const toDate = ref('');
const selectedTagFilterIds = ref<string[]>([]);
const noteSearch = ref('');
const newTagName = ref('');
const filterError = ref<string | null>(null);

// Tag autocomplete state
const tagSearchInput = ref('');
const tagDropdownOpen = ref(false);
const tagHighlightIndex = ref(-1);

const onTagSearchBlur = () => { setTimeout(() => { tagDropdownOpen.value = false; }, 150); };

watch(tagSearchInput, () => { tagHighlightIndex.value = -1; });

const filteredTagSuggestions = computed(() =>
  tags.value.filter(t =>
    !selectedTagFilterIds.value.includes(t.id) &&
    t.name.toLowerCase().includes(tagSearchInput.value.toLowerCase())
  )
);

const selectedTags = computed(() =>
  tags.value.filter(t => selectedTagFilterIds.value.includes(t.id))
);

const addTagFilter = (tag: TagResponse) => {
  if (!selectedTagFilterIds.value.includes(tag.id)) {
    selectedTagFilterIds.value = [...selectedTagFilterIds.value, tag.id];
  }
  tagSearchInput.value = '';
  tagHighlightIndex.value = -1;
  tagDropdownOpen.value = true;
};

const removeTagFilter = (tagId: string) => {
  selectedTagFilterIds.value = selectedTagFilterIds.value.filter(id => id !== tagId);
};

const onTagSearchKeydown = (e: KeyboardEvent) => {
  const suggestions = filteredTagSuggestions.value;
  if (e.key === 'ArrowDown') {
    e.preventDefault();
    tagDropdownOpen.value = true;
    tagHighlightIndex.value = Math.min(tagHighlightIndex.value + 1, suggestions.length - 1);
    return;
  }
  if (e.key === 'ArrowUp') {
    e.preventDefault();
    tagHighlightIndex.value = Math.max(tagHighlightIndex.value - 1, -1);
    return;
  }
  if (e.key === 'Enter' && suggestions.length > 0) {
    e.preventDefault();
    const idx = tagHighlightIndex.value >= 0 ? Math.min(tagHighlightIndex.value, suggestions.length - 1) : 0;
    if (suggestions[idx]) addTagFilter(suggestions[idx]);
    return;
  }
  if (e.key === 'Backspace' && tagSearchInput.value === '' && selectedTagFilterIds.value.length > 0) {
    selectedTagFilterIds.value = selectedTagFilterIds.value.slice(0, -1);
    return;
  }
  if (e.key === 'Escape') {
    e.stopPropagation();
    tagDropdownOpen.value = false;
    tagHighlightIndex.value = -1;
  }
};

const buildQuery = () => ({
  journalId: props.journal.id,
  from: fromDate.value ? new Date(fromDate.value).toISOString() : undefined,
  to: toDate.value ? new Date(toDate.value).toISOString() : undefined,
  tagIds: selectedTagFilterIds.value.length > 0 ? selectedTagFilterIds.value : undefined,
  noteSearch: noteSearch.value.trim() || undefined,
});

const fetchTransactions = async () => {
  try {
    loading.value = true;
    const query = buildQuery();
    const [balances, grouped, spending] = await Promise.all([
      transactionService.getSummary(query),
      transactionService.getGrouped(query),
      transactionService.getSpendingByTag(query)
    ]);
    summary.value = balances;
    groupedTransactions.value = grouped;
    spendingByTag.value = spending;
    error.value = null;
    initDefaultCollapse(grouped);
  } catch (err) {
    console.error('Failed to fetch transactions:', err);
    error.value = 'Failed to load transactions. Is the API running?';
  } finally {
    loading.value = false;
  }
};

const fetchTags = async () => {
  try {
    tags.value = await transactionService.getTags();
    tagError.value = null;
  } catch (err) {
    console.error('Failed to fetch tags:', err);
    tagError.value = 'Failed to load tags.';
  }
};

const onTransactionSaved = () => {
  editingTransaction.value = null;
  showAddForm.value = false;
  void fetchTransactions();
};

const collapsedYears = ref<Record<number, boolean>>({});
const collapsedMonths = ref<Record<string, boolean>>({});

const initDefaultCollapse = (groups: TransactionYearGroupResponse[]) => {
  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonth = now.getMonth() + 1;
  for (const yearGroup of groups) {
    for (const monthGroup of yearGroup.months) {
      const key = `${yearGroup.year}-${monthGroup.month}`;
      if (!(key in collapsedMonths.value)) {
        collapsedMonths.value[key] = !(yearGroup.year === currentYear && monthGroup.month === currentMonth);
      }
    }
  }
};

const hasActiveFilters = computed(() =>
  Boolean(fromDate.value || toDate.value || selectedTagFilterIds.value.length > 0 || noteSearch.value.trim())
);

const handleGlobalKeydown = (e: KeyboardEvent) => {
  if (e.key !== 'Escape') return;
  if (showAddForm.value) {
    showAddForm.value = false;
    editingTransaction.value = null;
  } else if (showTagsModal.value) {
    showTagsModal.value = false;
  }
};

onMounted(async () => {
  window.addEventListener('keydown', handleGlobalKeydown);
  await Promise.all([fetchTransactions(), fetchTags()]);
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalKeydown);
});

const formatCurrency = (amount: number, currency: string) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency,
  }).format(amount);
};

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });
};

const formatTransactionValue = (transaction: TransactionResponse) => {
  return `${transaction.type === TransactionType.In ? '+' : '-'} ${formatCurrency(transaction.amount, transaction.currency)}`;
};

const formatRollups = (rollups: { currency: string; netChange: number }[]) =>
  rollups.map(r => `${r.currency} ${r.netChange >= 0 ? '+' : ''}${formatCurrency(r.netChange, r.currency)}`).join(' · ');

const toggleYear = (year: number) => {
  collapsedYears.value[year] = !collapsedYears.value[year];
};

const toggleMonth = (year: number, month: number) => {
  const key = `${year}-${month}`;
  collapsedMonths.value[key] = !collapsedMonths.value[key];
};

const foldAllMonths = (year: number, yearGroup: TransactionYearGroupResponse, collapse: boolean) => {
  for (const monthGroup of yearGroup.months) {
    collapsedMonths.value[`${year}-${monthGroup.month}`] = collapse;
  }
};

const areAllMonthsCollapsed = (year: number, yearGroup: TransactionYearGroupResponse) =>
  yearGroup.months.every(m => collapsedMonths.value[`${year}-${m.month}`]);

const startCreate = () => {
  editingTransaction.value = null;
  showAddForm.value = true;
};

const startEdit = (transaction: TransactionResponse) => {
  editingTransaction.value = transaction;
  showAddForm.value = true;
};

const removeTransaction = async (transaction: TransactionResponse) => {
  if (!confirm(`Delete transaction "${transaction.note ?? 'Untitled Transaction'}"?`)) {
    return;
  }

  try {
    await transactionService.delete(transaction.id, props.journal.id);
    await fetchTransactions();
  } catch (err) {
    console.error('Failed to delete transaction:', err);
    error.value = 'Failed to delete transaction. Please try again.';
  }
};

const applyFilters = () => {
  if (fromDate.value && toDate.value && new Date(fromDate.value) > new Date(toDate.value)) {
    filterError.value = 'The "From" date must be earlier than or equal to the "To" date.';
    return;
  }

  filterError.value = null;
  void fetchTransactions();
};

const clearFilters = () => {
  fromDate.value = '';
  toDate.value = '';
  selectedTagFilterIds.value = [];
  noteSearch.value = '';
  tagSearchInput.value = '';
  filterError.value = null;
  void fetchTransactions();
};

const createTag = async () => {
  const name = newTagName.value.trim();
  if (!name) {
    return;
  }

  try {
    await transactionService.createTag({ name });
    newTagName.value = '';
    await fetchTags();
  } catch (err) {
    console.error('Failed to create tag:', err);
    tagError.value = 'Failed to create tag.';
  }
};

const removeTag = async (tag: TagResponse) => {
  if (!confirm(`Delete tag "${tag.name}"?`)) {
    return;
  }

  try {
    await transactionService.deleteTag(tag.id);
    selectedTagFilterIds.value = selectedTagFilterIds.value.filter(x => x !== tag.id);
    await Promise.all([fetchTags(), fetchTransactions()]);
  } catch (err) {
    console.error('Failed to delete tag:', err);
    tagError.value = 'Failed to delete tag. It may still be used by existing transactions.';
  }
};
</script>

<template>
  <div class="space-y-6">
    <!-- Closed journal banner -->
    <div v-if="journal.isClosed" class="bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-800 text-amber-700 dark:text-amber-400 p-3 rounded-lg text-sm flex items-center gap-2">
      🔒 This journal is closed. Transactions cannot be added, edited, or deleted.
    </div>

    <!-- Summary Card -->
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

    <!-- Actions -->
    <div class="flex justify-between items-center">
      <h2 class="text-xl font-bold text-gray-800 dark:text-gray-100">Recent Transactions</h2>
      <div class="flex gap-2">
        <button 
          class="bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 px-4 py-2 rounded-lg font-medium transition-colors flex items-center gap-1.5"
          @click="showTagsModal = true"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 6h.008v.008H6V6z" />
          </svg>
          Manage Tags
        </button>
        <button 
          :disabled="loading || journal.isClosed"
          class="bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg font-medium transition-colors shadow-sm flex items-center disabled:opacity-50"
          :title="journal.isClosed ? 'Journal is closed' : ''"
          @click="startCreate"
        >
          <span class="mr-1">+</span> New Transaction
        </button>
      </div>
    </div>

    <div class="bg-white dark:bg-gray-800 p-4 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 space-y-3">
      <div class="grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">From</label>
          <input v-model="fromDate" type="date" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500" />
        </div>
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">To</label>
          <input v-model="toDate" type="date" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500" />
        </div>
        <div class="md:col-span-2">
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Search description</label>
          <input v-model="noteSearch" type="text" placeholder="Type to search…" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500" @keydown.enter="applyFilters" />
        </div>
        <div class="flex gap-2">
          <button :disabled="loading" class="flex-1 px-3 py-2 rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 text-sm" @click="applyFilters">Apply</button>
          <button :disabled="loading" class="flex-1 px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 text-sm" @click="clearFilters">Clear</button>
        </div>
      </div>
      <!-- Tag autocomplete filter -->
      <div v-if="tags.length > 0" class="border-t border-gray-100 dark:border-gray-700 pt-3">
        <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wide">Filter by tags</label>
        <div
          class="flex flex-wrap gap-1.5 min-h-[36px] px-2 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 bg-white cursor-text"
          @click="($event.target as HTMLElement).closest('div')?.querySelector('input')?.focus()"
        >
          <span
            v-for="tag in selectedTags"
            :key="`chip-${tag.id}`"
            class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium flex-shrink-0"
            :style="{ backgroundColor: tag.color + '26', color: tag.color }"
          >
            {{ tag.name }}
            <button class="opacity-60 hover:opacity-100 leading-none" @click.stop="removeTagFilter(tag.id)">×</button>
          </span>
          <div class="relative flex-1 min-w-[120px]">
            <input
              v-model="tagSearchInput"
              type="text"
              placeholder="Type tag name…"
              class="w-full bg-transparent text-sm text-gray-700 dark:text-gray-200 outline-none py-0.5"
              @focus="tagDropdownOpen = true; tagHighlightIndex = -1"
              @blur="onTagSearchBlur"
              @keydown="onTagSearchKeydown"
            />
            <div
              v-if="tagDropdownOpen && filteredTagSuggestions.length > 0"
              class="absolute top-full left-0 z-20 mt-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg min-w-[180px] max-h-48 overflow-y-auto"
            >
              <button
                v-for="(tag, idx) in filteredTagSuggestions"
                :key="`suggest-${tag.id}`"
                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 transition-colors"
                :class="idx === tagHighlightIndex
                  ? 'bg-indigo-50 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300'
                  : 'hover:bg-gray-50 dark:hover:bg-gray-700'"
                @mousedown.prevent="addTagFilter(tag)"
                @mouseover="tagHighlightIndex = idx"
              >
                <span class="w-2.5 h-2.5 rounded-full flex-shrink-0" :style="{ backgroundColor: tag.color }" />
                <span class="text-gray-800 dark:text-gray-200">{{ tag.name }}</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="filterError" class="bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-800 text-amber-700 dark:text-amber-400 p-3 rounded-lg text-sm">
      {{ filterError }}
    </div>

    <!-- Add/Edit Modal -->
    <Teleport to="body">
      <div
        v-if="showAddForm"
        class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
        @click.self="showAddForm = false; editingTransaction = null"
      >
        <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700">
            <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">{{ editingTransaction ? 'Edit Transaction' : 'New Transaction' }}</h2>
            <button class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none" aria-label="Close" @click="showAddForm = false; editingTransaction = null">×</button>
          </div>
          <div class="p-6">
            <AddTransactionForm 
              :transaction="editingTransaction"
              :availableTags="tags"
              :journalId="journal.id"
              :currency="journal.primaryCurrency"
              @transaction-saved="onTransactionSaved"
              @cancel="showAddForm = false; editingTransaction = null"
            />
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Manage Tags Modal -->
    <Teleport to="body">
      <div
        v-if="showTagsModal"
        class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
        @click.self="showTagsModal = false"
      >
        <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-md max-h-[80vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700">
            <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Manage Tags</h2>
            <button class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none" aria-label="Close" @click="showTagsModal = false">×</button>
          </div>
          <div class="p-6 space-y-4">
            <div class="flex gap-2">
              <input
                v-model="newTagName"
                type="text"
                maxlength="60"
                placeholder="New tag name"
                class="flex-1 px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
                @keydown.enter="createTag"
              />
              <button class="px-4 py-2 rounded-lg bg-indigo-600 text-white hover:bg-indigo-700" @click="createTag">Add</button>
            </div>
            <div v-if="tagError" class="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 p-2 rounded text-sm">
              {{ tagError }}
            </div>
            <div class="flex flex-wrap gap-2">
              <span
                v-for="tag in tags"
                :key="`manage-${tag.id}`"
                class="inline-flex items-center gap-2 px-2.5 py-1 rounded-full text-sm font-medium"
                :style="{ backgroundColor: tag.color + '26', color: tag.color }"
              >
                {{ tag.name }}
                <button class="opacity-60 hover:opacity-100" @click="removeTag(tag)">×</button>
              </span>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Transaction List -->
    <div v-if="loading" class="text-center py-12">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-4 border-indigo-500 border-t-transparent"></div>
      <p class="mt-2 text-gray-500 dark:text-gray-400">Loading your budget...</p>
    </div>

    <div v-else-if="error" class="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 p-4 rounded-lg">
      {{ error }}
    </div>

    <div v-else-if="groupedTransactions.length === 0" class="bg-white dark:bg-gray-800 p-12 rounded-xl border border-dashed border-gray-300 dark:border-gray-600 text-center">
      <p class="text-gray-500 dark:text-gray-400">
        {{ hasActiveFilters ? 'No transactions match the current filters.' : 'No transactions recorded yet. Start by adding one!' }}
      </p>
      <button
        v-if="hasActiveFilters"
        class="mt-3 px-4 py-2 rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
        @click="clearFilters"
      >
        Clear filters
      </button>
    </div>

    <div v-else class="space-y-4">
      <div v-for="yearGroup in groupedTransactions" :key="`year-${yearGroup.year}`" class="bg-white dark:bg-gray-800 shadow-sm border border-gray-100 dark:border-gray-700 rounded-xl overflow-hidden">
        <div class="flex items-center px-4 py-3 bg-gray-50 dark:bg-gray-900/50">
          <button class="flex-1 text-left font-semibold text-gray-800 dark:text-gray-100 flex items-center gap-3 min-w-0" @click="toggleYear(yearGroup.year)">
            <span class="flex-shrink-0">{{ yearGroup.year }}</span>
            <span v-if="collapsedYears[yearGroup.year]" class="text-xs font-normal text-gray-500 dark:text-gray-400 truncate">{{ formatRollups(yearGroup.rollups) }}</span>
          </button>
          <div class="flex items-center gap-2 flex-shrink-0">
            <button
              v-if="!collapsedYears[yearGroup.year]"
              class="text-xs px-2 py-1 rounded border border-gray-200 dark:border-gray-600 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
              :title="areAllMonthsCollapsed(yearGroup.year, yearGroup) ? 'Expand all months' : 'Collapse all months'"
              @click.stop="foldAllMonths(yearGroup.year, yearGroup, !areAllMonthsCollapsed(yearGroup.year, yearGroup))"
            >{{ areAllMonthsCollapsed(yearGroup.year, yearGroup) ? '≡↓' : '≡↑' }}</button>
            <button class="text-gray-500 dark:text-gray-400 w-6 text-center font-bold" @click="toggleYear(yearGroup.year)">{{ collapsedYears[yearGroup.year] ? '+' : '−' }}</button>
          </div>
        </div>
        <div v-if="!collapsedYears[yearGroup.year]" class="p-4 space-y-3">
          <div class="text-xs text-gray-600 dark:text-gray-400 flex gap-3 flex-wrap">
            <span v-for="rollup in yearGroup.rollups" :key="`year-rollup-${yearGroup.year}-${rollup.currency}`">
              {{ rollup.currency }} net: {{ formatCurrency(rollup.netChange, rollup.currency) }}
            </span>
          </div>
          <div v-for="monthGroup in yearGroup.months" :key="`month-${yearGroup.year}-${monthGroup.month}`" class="border border-gray-100 dark:border-gray-700 rounded-lg overflow-hidden">
            <button class="w-full px-3 py-2 text-left bg-white dark:bg-gray-800 flex justify-between items-center text-sm font-medium text-gray-700 dark:text-gray-300" @click="toggleMonth(yearGroup.year, monthGroup.month)">
              <span class="flex items-center gap-3 min-w-0">
                <span class="flex-shrink-0">{{ new Date(yearGroup.year, monthGroup.month - 1, 1).toLocaleString(undefined, { month: 'long' }) }}</span>
                <span v-if="collapsedMonths[`${yearGroup.year}-${monthGroup.month}`]" class="text-xs font-normal text-gray-500 dark:text-gray-400 truncate">{{ formatRollups(monthGroup.rollups) }}</span>
              </span>
              <span class="flex-shrink-0">{{ collapsedMonths[`${yearGroup.year}-${monthGroup.month}`] ? '+' : '−' }}</span>
            </button>
            <div v-if="!collapsedMonths[`${yearGroup.year}-${monthGroup.month}`]" class="divide-y divide-gray-100 dark:divide-gray-700">
              <div class="px-3 py-2 text-xs text-gray-600 dark:text-gray-400 flex gap-3 flex-wrap bg-gray-50 dark:bg-gray-900/30">
                <span v-for="rollup in monthGroup.rollups" :key="`month-rollup-${yearGroup.year}-${monthGroup.month}-${rollup.currency}`">
                  {{ rollup.currency }} net: {{ formatCurrency(rollup.netChange, rollup.currency) }}
                </span>
              </div>
              <div v-for="t in monthGroup.transactions" :key="t.id" class="px-4 py-3 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
                <div class="flex items-center gap-3 min-w-0">
                  <span class="text-sm text-gray-500 dark:text-gray-400 w-24 flex-shrink-0">{{ formatDate(t.occurredAt) }}</span>
                  <div class="flex-1 min-w-0 flex items-center gap-2">
                    <span class="font-medium text-gray-900 dark:text-gray-100 truncate min-w-0 shrink">{{ t.note || 'Untitled Transaction' }}</span>
                    <div v-if="t.tags.length" class="flex-shrink-0 flex gap-1 flex-wrap">
                      <span
                        v-for="tag in t.tags"
                        :key="`${t.id}-${tag.id}`"
                        class="text-xs px-2 py-0.5 rounded-full whitespace-nowrap font-medium"
                        :style="{ backgroundColor: tag.color + '26', color: tag.color }"
                      >
                        {{ tag.name }}
                      </span>
                    </div>
                  </div>
                  <span class="w-28 text-right font-bold flex-shrink-0" :class="t.type === TransactionType.In ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'">
                    {{ formatTransactionValue(t) }}
                  </span>
                  <span class="w-10 text-center text-xs uppercase tracking-widest font-semibold px-1 py-0.5 rounded bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 flex-shrink-0">{{ t.currency }}</span>
                  <div class="flex gap-1 flex-shrink-0">
                    <button
                      v-if="!journal.isClosed"
                      class="p-1.5 rounded hover:bg-indigo-50 dark:hover:bg-indigo-900/40 text-gray-400 hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors"
                      @click="startEdit(t)"
                      title="Edit"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125" />
                      </svg>
                    </button>
                    <button
                      v-if="!journal.isClosed"
                      class="p-1.5 rounded hover:bg-red-50 dark:hover:bg-red-900/30 text-gray-400 hover:text-red-600 dark:hover:text-red-400 transition-colors"
                      @click="removeTransaction(t)"
                      title="Delete"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                      </svg>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="bg-white dark:bg-gray-800 shadow-sm border border-gray-100 dark:border-gray-700 rounded-xl p-4">
        <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Spending by Tag</h3>
        <div v-if="!spendingByTag || spendingByTag.items.length === 0" class="text-sm text-gray-500 dark:text-gray-400">No spending data in the selected period.</div>
        <div v-else>
          <div v-for="item in spendingByTag.items.slice(0, 5)" :key="`spend-${item.tagId ?? 'untagged'}`" class="mb-2">
            <div class="flex justify-between text-xs text-gray-700 dark:text-gray-300">
              <span>{{ item.tagName }}</span>
              <span>{{ formatCurrency(item.totalOut, journal.primaryCurrency) }}</span>
            </div>
            <div class="h-2 rounded bg-gray-100 dark:bg-gray-700">
              <div
                class="h-2 rounded bg-indigo-500"
                :style="{ width: `${spendingByTag.items[0]?.totalOut ? (item.totalOut / spendingByTag.items[0].totalOut) * 100 : 0}%` }"
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
