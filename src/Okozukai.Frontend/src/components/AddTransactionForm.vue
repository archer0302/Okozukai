<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { PropType } from 'vue';
import { transactionService } from '../api/transactionService';
import { TransactionType, type TagResponse, type TransactionResponse } from '../types/transaction';

const props = defineProps({
  transaction: {
    type: Object as PropType<TransactionResponse | null>,
    default: null
  },
  availableTags: {
    type: Array as PropType<TagResponse[]>,
    default: () => []
  },
  journalId: {
    type: String,
    required: true
  },
  currency: {
    type: String,
    required: true
  }
});

const emit = defineEmits(['transaction-saved', 'cancel']);

const type = ref<TransactionType>(TransactionType.Out);
const amount = ref<number | null>(null);
const occurredAt = ref(new Date().toISOString().slice(0, 10));
const note = ref('');
const selectedTagIds = ref<string[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);
const fieldError = ref<string | null>(null);
const isEditMode = computed(() => props.transaction !== null);

const toDateTimeInputValue = (value: string) => new Date(value).toISOString().slice(0, 10);

watch(
  () => props.transaction,
  (transaction) => {
    if (!transaction) {
      type.value = TransactionType.Out;
      amount.value = null;
      occurredAt.value = new Date().toISOString().slice(0, 10);
      note.value = '';
      selectedTagIds.value = [];
      return;
    }

    type.value = transaction.type;
    amount.value = transaction.amount;
    occurredAt.value = toDateTimeInputValue(transaction.occurredAt);
    note.value = transaction.note ?? '';
    selectedTagIds.value = transaction.tags.map(x => x.id);
  },
  { immediate: true }
);

const handleSubmit = async () => {
  fieldError.value = null;

  if (!occurredAt.value || Number.isNaN(new Date(occurredAt.value).getTime())) {
    fieldError.value = 'Please provide a valid date.';
    return;
  }

  if (amount.value === null || amount.value <= 0) {
    fieldError.value = 'Amount must be greater than zero.';
    return;
  }

  try {
    loading.value = true;
    error.value = null;

    if (isEditMode.value && props.transaction) {
      await transactionService.update(props.transaction.id, {
        type: type.value,
        amount: amount.value,
        occurredAt: new Date(occurredAt.value).toISOString(),
        note: note.value.trim() || null,
        tagIds: selectedTagIds.value
      });
    } else {
      await transactionService.create({
        journalId: props.journalId,
        type: type.value,
        amount: amount.value,
        occurredAt: new Date(occurredAt.value).toISOString(),
        note: note.value.trim() || null,
        tagIds: selectedTagIds.value
      });
    }

    emit('transaction-saved');
  } catch (err: any) {
    console.error('Failed to save transaction:', err);
    error.value = err.response?.data?.detail || 'Failed to save transaction. Please try again.';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="bg-white dark:bg-gray-800 p-6 rounded-xl shadow-md border border-gray-100 dark:border-gray-700">
    <h3 class="text-lg font-bold text-gray-900 dark:text-gray-100 mb-4">{{ isEditMode ? 'Edit Transaction' : 'Add Transaction' }}</h3>
    
    <form @submit.prevent="handleSubmit" class="space-y-4">
      <div v-if="error" class="bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400 p-3 rounded-lg text-sm border border-red-100 dark:border-red-800">
        {{ error }}
      </div>
      <div v-if="fieldError" class="bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400 p-3 rounded-lg text-sm border border-amber-100 dark:border-amber-800">
        {{ fieldError }}
      </div>

      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Type</label>
          <select 
            v-model="type"
            class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          >
            <option :value="TransactionType.In">Income (+)</option>
            <option :value="TransactionType.Out">Expense (-)</option>
          </select>
        </div>
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Currency</label>
          <div class="w-full px-3 py-2 rounded-lg border border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700/50 text-gray-600 dark:text-gray-400 font-medium uppercase">
            {{ currency }}
          </div>
        </div>
      </div>

      <div>
        <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Amount</label>
        <div class="relative mt-1 rounded-md shadow-sm">
          <input 
            type="number" 
            step="0.01" 
            v-model="amount"
            class="block w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
            placeholder="0.00"
            required
          />
        </div>
      </div>

      <div>
        <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Date</label>
        <input 
          type="date" 
          v-model="occurredAt"
          class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          required
        />
      </div>

      <div>
        <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Note (Optional)</label>
        <textarea 
          v-model="note"
          rows="2"
          class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          placeholder="What was this for?"
        ></textarea>
      </div>

      <div>
        <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Tags</label>
        <div v-if="availableTags.length === 0" class="text-sm text-gray-500 dark:text-gray-400">
          No tags yet. Create tags from the dashboard first.
        </div>
        <div v-else class="flex flex-wrap gap-2">
          <label
            v-for="tag in availableTags"
            :key="tag.id"
            class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full border cursor-pointer select-none transition-colors text-sm font-medium text-gray-700 dark:text-gray-200"
            :class="selectedTagIds.includes(tag.id) ? 'border-transparent' : 'border-gray-200 dark:border-gray-600 hover:border-gray-300 dark:hover:border-gray-500'"
            :style="selectedTagIds.includes(tag.id)
              ? { backgroundColor: tag.color + '26', color: tag.color, borderColor: tag.color }
              : {}"
          >
            <input v-model="selectedTagIds" type="checkbox" :value="tag.id" class="hidden" />
            <span class="w-2 h-2 rounded-full flex-shrink-0" :style="{ backgroundColor: tag.color }" />
            <span>{{ tag.name }}</span>
          </label>
        </div>
      </div>

      <div class="flex space-x-3 pt-2">
        <button 
          type="button"
          @click="$emit('cancel')"
          class="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg font-medium hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
        >
          Cancel
        </button>
        <button 
          type="submit"
          :disabled="loading"
          class="flex-1 px-4 py-2 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors shadow-sm"
        >
          {{ loading ? 'Saving...' : (isEditMode ? 'Update' : 'Save') }}
        </button>
      </div>
    </form>
  </div>
</template>
