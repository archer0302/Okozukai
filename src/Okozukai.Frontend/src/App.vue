<script setup lang="ts">
import { ref, onMounted, provide, computed, readonly } from 'vue';
import { useRoute } from 'vue-router';
import { journalService } from './api/journalService';
import type { JournalResponse } from './types/transaction';

const route = useRoute();

const isDark = ref(false);
const journals = ref<JournalResponse[]>([]);
const currentJournal = ref<JournalResponse | null>(null);
const showJournalModal = ref(false);
const journalModalMode = ref<'create' | 'edit' | 'manage'>('manage');
const editingJournal = ref<JournalResponse | null>(null);
const journalNameInput = ref('');
const journalCurrencyInput = ref('');
const journalError = ref<string | null>(null);
const journalLoading = ref(false);

provide('currentJournal', currentJournal);
provide('journals', journals);
provide('isDark', readonly(isDark));

const currentRouteName = computed(() => route.name as string);

onMounted(async () => {
  isDark.value = localStorage.getItem('theme') === 'dark' ||
    (!localStorage.getItem('theme') && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.classList.toggle('dark', isDark.value);
  await loadJournals();
});

const loadJournals = async () => {
  try {
    journals.value = await journalService.getAll();
    const lastId = localStorage.getItem('lastSelectedJournalId');
    const found = lastId ? journals.value.find(j => j.id === lastId) : null;
    currentJournal.value = found ?? journals.value[0] ?? null;
    if (currentJournal.value) {
      localStorage.setItem('lastSelectedJournalId', currentJournal.value.id);
    }
  } catch (err) {
    console.error('Failed to load journals:', err);
  }
};

const selectJournal = (journal: JournalResponse) => {
  currentJournal.value = journal;
  localStorage.setItem('lastSelectedJournalId', journal.id);
};

const openCreateJournal = () => {
  journalModalMode.value = 'create';
  journalNameInput.value = '';
  journalCurrencyInput.value = '';
  journalError.value = null;
  showJournalModal.value = true;
};

const openManageJournals = () => {
  journalModalMode.value = 'manage';
  journalError.value = null;
  showJournalModal.value = true;
};

const openEditJournal = (journal: JournalResponse) => {
  journalModalMode.value = 'edit';
  editingJournal.value = journal;
  journalNameInput.value = journal.name;
  journalError.value = null;
  showJournalModal.value = true;
};

const closeJournalModal = () => {
  showJournalModal.value = false;
  editingJournal.value = null;
  journalError.value = null;
};

const saveJournal = async () => {
  const name = journalNameInput.value.trim();
  if (!name) { journalError.value = 'Journal name is required.'; return; }

  journalLoading.value = true;
  journalError.value = null;
  try {
    if (journalModalMode.value === 'create') {
      const currency = journalCurrencyInput.value.trim().toUpperCase();
      if (currency.length !== 3) { journalError.value = 'Currency must be a 3-letter code (e.g. USD).'; return; }
      const created = await journalService.create({ name, primaryCurrency: currency });
      journals.value = [...journals.value, created];
      currentJournal.value = created;
      localStorage.setItem('lastSelectedJournalId', created.id);
      closeJournalModal();
    } else if (journalModalMode.value === 'edit' && editingJournal.value) {
      const updated = await journalService.update(editingJournal.value.id, { name });
      journals.value = journals.value.map(j => j.id === updated.id ? updated : j);
      if (currentJournal.value?.id === updated.id) currentJournal.value = updated;
      closeJournalModal();
    }
  } catch (err: any) {
    journalError.value = err.response?.data?.detail || 'Failed to save journal.';
  } finally {
    journalLoading.value = false;
  }
};

const toggleJournalClosed = async (journal: JournalResponse) => {
  try {
    const updated = journal.isClosed
      ? await journalService.reopen(journal.id)
      : await journalService.close(journal.id);
    journals.value = journals.value.map(j => j.id === updated.id ? updated : j);
    if (currentJournal.value?.id === updated.id) currentJournal.value = updated;
  } catch (err: any) {
    journalError.value = err.response?.data?.detail || 'Failed to update journal status.';
  }
};

const deleteJournal = async (journal: JournalResponse) => {
  if (!journal.isClosed) { journalError.value = 'Close the journal before deleting it.'; return; }
  if (!confirm(`Delete journal "${journal.name}" and ALL its transactions? This cannot be undone.`)) return;
  try {
    await journalService.delete(journal.id);
    journals.value = journals.value.filter(j => j.id !== journal.id);
    if (currentJournal.value?.id === journal.id) {
      currentJournal.value = journals.value[0] ?? null;
      if (currentJournal.value) localStorage.setItem('lastSelectedJournalId', currentJournal.value.id);
      else localStorage.removeItem('lastSelectedJournalId');
    }
  } catch (err: any) {
    journalError.value = err.response?.data?.detail || 'Failed to delete journal.';
  }
};

const toggleDark = () => {
  isDark.value = !isDark.value;
  document.documentElement.classList.toggle('dark', isDark.value);
  localStorage.setItem('theme', isDark.value ? 'dark' : 'light');
};
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 w-full transition-colors duration-200">
    <header class="bg-indigo-600 dark:bg-indigo-800 text-white shadow-lg p-4">
      <div class="max-w-4xl mx-auto flex justify-between items-center gap-4 flex-wrap">
        <h1 class="text-2xl font-bold tracking-tight flex-shrink-0">Okozukai</h1>

        <!-- Journal selector -->
        <div class="flex items-center gap-2 flex-1 min-w-0">
          <div v-if="journals.length > 0" class="flex items-center gap-1 bg-white/10 rounded-lg px-3 py-1.5 min-w-0">
            <select
              class="bg-transparent text-white font-medium text-sm focus:outline-none cursor-pointer max-w-[180px] truncate"
              :value="currentJournal?.id"
              @change="selectJournal(journals.find(j => j.id === ($event.target as HTMLSelectElement).value)!)"
            >
              <option v-for="j in journals" :key="j.id" :value="j.id" class="text-gray-900">
                {{ j.isClosed ? '🔒 ' : '' }}{{ j.name }} ({{ j.primaryCurrency }})
              </option>
            </select>
            <span v-if="currentJournal?.isClosed" class="text-xs bg-white/20 px-1.5 py-0.5 rounded flex-shrink-0">Closed</span>
          </div>
          <button
            v-if="journals.length > 0"
            class="text-white/80 hover:text-white text-sm px-2 py-1 rounded hover:bg-white/10 transition-colors flex-shrink-0"
            @click="openManageJournals"
            title="Manage journals"
          >⚙</button>
          <button
            class="text-white/80 hover:text-white text-sm px-2 py-1 rounded hover:bg-white/10 transition-colors flex-shrink-0"
            @click="openCreateJournal"
            title="New journal"
          >+ Journal</button>
        </div>

        <div class="flex items-center gap-4 flex-shrink-0">
          <button
            @click="toggleDark"
            class="p-2 rounded-lg bg-white/10 hover:bg-white/20 transition-colors"
            :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
          >
            <span v-if="isDark">☀️</span>
            <span v-else>🌙</span>
          </button>
        </div>
      </div>
    </header>

    <main class="max-w-4xl mx-auto p-4 sm:p-6 lg:p-8">
      <!-- Navigation tabs -->
      <nav v-if="journals.length > 0 && currentJournal" class="flex gap-1 mb-6 bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700 p-1">
        <router-link
          :to="{ name: 'transactions' }"
          class="flex-1 text-center px-4 py-2 rounded-lg text-sm font-medium transition-colors"
          :class="currentRouteName === 'transactions'
            ? 'bg-indigo-600 text-white shadow-sm'
            : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700'"
        >
          Transactions
        </router-link>
        <router-link
          :to="{ name: 'dashboard' }"
          class="flex-1 text-center px-4 py-2 rounded-lg text-sm font-medium transition-colors"
          :class="currentRouteName === 'dashboard'
            ? 'bg-indigo-600 text-white shadow-sm'
            : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700'"
        >
          Dashboard
        </router-link>
      </nav>

      <!-- No journals state -->
      <div v-if="journals.length === 0" class="bg-white dark:bg-gray-800 p-12 rounded-xl border border-dashed border-gray-300 dark:border-gray-600 text-center">
        <p class="text-gray-500 dark:text-gray-400 mb-4">No journals yet. Create your first journal to start tracking your budget.</p>
        <button class="px-6 py-2 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700" @click="openCreateJournal">
          Create Journal
        </button>
      </div>
      <router-view
        v-else-if="currentJournal"
        :key="currentJournal.id"
        :journal="currentJournal"
        @journal-updated="loadJournals"
      />
    </main>

    <!-- Journal Create/Edit Modal -->
    <Teleport to="body">
      <div
        v-if="showJournalModal"
        class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
        @click.self="closeJournalModal"
      >
        <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-md">
          <!-- Manage mode -->
          <template v-if="journalModalMode === 'manage'">
            <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Manage Journals</h2>
              <button class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none" @click="closeJournalModal">×</button>
            </div>
            <div class="p-6 space-y-3 max-h-[60vh] overflow-y-auto">
              <div v-if="journalError" class="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 p-2 rounded text-sm">
                {{ journalError }}
              </div>
              <div
                v-for="j in journals"
                :key="j.id"
                class="flex items-center gap-2 p-3 rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50"
              >
                <div class="flex-1 min-w-0">
                  <div class="font-medium text-gray-900 dark:text-gray-100 truncate">{{ j.name }}</div>
                  <div class="text-xs text-gray-500 dark:text-gray-400">{{ j.primaryCurrency }} · {{ j.isClosed ? 'Closed' : 'Open' }}</div>
                </div>
                <button
                  class="text-xs px-2 py-1 rounded border border-gray-200 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 flex-shrink-0"
                  :title="j.isClosed ? 'Reopen journal' : 'Close journal'"
                  @click="toggleJournalClosed(j)"
                >{{ j.isClosed ? '↩ Reopen' : '🔒 Close' }}</button>
                <button
                  class="text-xs px-2 py-1 rounded border border-gray-200 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 flex-shrink-0"
                  @click="openEditJournal(j)"
                >✏ Edit</button>
                <button
                  class="text-xs px-2 py-1 rounded border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/30 flex-shrink-0 disabled:opacity-40"
                  :disabled="!j.isClosed"
                  :title="j.isClosed ? 'Delete journal' : 'Close journal before deleting'"
                  @click="deleteJournal(j)"
                >🗑</button>
              </div>
            </div>
            <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-700">
              <button class="w-full px-4 py-2 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700" @click="openCreateJournal">
                + New Journal
              </button>
            </div>
          </template>

          <!-- Create/Edit mode -->
          <template v-else>
            <div class="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-700">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">{{ journalModalMode === 'create' ? 'New Journal' : 'Edit Journal' }}</h2>
              <button class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none" @click="closeJournalModal">×</button>
            </div>
            <div class="p-6 space-y-4">
              <div v-if="journalError" class="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 p-2 rounded text-sm">
                {{ journalError }}
              </div>
              <div>
                <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Name</label>
                <input
                  v-model="journalNameInput"
                  type="text"
                  maxlength="100"
                  placeholder="My Budget"
                  class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
                  @keydown.enter="saveJournal"
                />
              </div>
              <div v-if="journalModalMode === 'create'">
                <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">Currency</label>
                <input
                  v-model="journalCurrencyInput"
                  type="text"
                  maxlength="3"
                  placeholder="USD"
                  class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-100 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 uppercase"
                  @input="journalCurrencyInput = journalCurrencyInput.toUpperCase()"
                  @keydown.enter="saveJournal"
                />
              </div>
              <div class="flex gap-3 pt-2">
                <button class="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700" @click="closeJournalModal">Cancel</button>
                <button class="flex-1 px-4 py-2 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700 disabled:opacity-50" :disabled="journalLoading" @click="saveJournal">
                  {{ journalLoading ? 'Saving…' : 'Save' }}
                </button>
              </div>
            </div>
          </template>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style>
/* Reset global styles from Vite template that might interfere with Tailwind */
#app {
  width: 100%;
  margin: 0;
  padding: 0;
  text-align: left;
}
</style>

