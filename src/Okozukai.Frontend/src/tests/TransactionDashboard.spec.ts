import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import TransactionDashboard from '../components/TransactionDashboard.vue';
import { transactionService } from '../api/transactionService';
import { TransactionType, type JournalResponse } from '../types/transaction';

const mockJournal: JournalResponse = {
  id: 'journal-1',
  name: 'My Budget',
  primaryCurrency: 'USD',
  isClosed: false,
  createdAt: new Date().toISOString()
};

// Mock the API service
vi.mock('../api/transactionService', () => ({
  transactionService: {
    getAll: vi.fn(),
    getSummary: vi.fn(),
    getGrouped: vi.fn(),
    getSpendingByTag: vi.fn(),
    getTags: vi.fn(),
    createTag: vi.fn(),
    deleteTag: vi.fn(),
    delete: vi.fn()
  }
}));

describe('TransactionDashboard.vue', () => {
  beforeEach(() => {
    vi.mocked(transactionService.getGrouped).mockResolvedValue([]);
    vi.mocked(transactionService.getSpendingByTag).mockResolvedValue({ currency: 'USD', items: [] });
    vi.mocked(transactionService.getTags).mockResolvedValue([]);
  });

  it('renders summary card with correct calculations', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue(
      { currency: 'USD', totalIn: 1000, totalOut: 250, net: 750 }
    );

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    const text = wrapper.text();
    expect(text).toContain('USD Balance');
    expect(text).toContain('$750.00');
  });

  it('shows a message when no transactions exist', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 0, net: 0 });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('No transactions recorded yet');
  });

  it('shows edit and delete actions for transactions in an open journal', async () => {
    const tx = { id: '1', journalId: 'journal-1', journalName: 'My Budget', currency: 'USD', type: TransactionType.In, amount: 100, occurredAt: new Date().toISOString(), note: 'Salary', tags: [] };
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 100, totalOut: 0, net: 100 });
    vi.mocked(transactionService.getGrouped).mockResolvedValue([
      { year: new Date(tx.occurredAt).getFullYear(), rollups: [], months: [{ year: new Date(tx.occurredAt).getFullYear(), month: new Date(tx.occurredAt).getMonth() + 1, transactions: [tx], rollups: [] }] }
    ]);

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.find('button[title="Edit"]').exists()).toBe(true);
    expect(wrapper.find('button[title="Delete"]').exists()).toBe(true);
  });

  it('hides edit and delete actions for a closed journal', async () => {
    const closedJournal: JournalResponse = { ...mockJournal, isClosed: true };
    const tx = { id: '1', journalId: 'journal-1', journalName: 'My Budget', currency: 'USD', type: TransactionType.In, amount: 100, occurredAt: new Date().toISOString(), note: 'Salary', tags: [] };
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 100, totalOut: 0, net: 100 });
    vi.mocked(transactionService.getGrouped).mockResolvedValue([
      { year: new Date(tx.occurredAt).getFullYear(), rollups: [], months: [{ year: new Date(tx.occurredAt).getFullYear(), month: new Date(tx.occurredAt).getMonth() + 1, transactions: [tx], rollups: [] }] }
    ]);

    const wrapper = mount(TransactionDashboard, { props: { journal: closedJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.find('button[title="Edit"]').exists()).toBe(false);
    expect(wrapper.find('button[title="Delete"]').exists()).toBe(false);
  });

  it('applies date filters through API query params', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 0, net: 0 });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    await wrapper.find('input[type="date"]').setValue('2026-01-01');
    await wrapper.findAll('input[type="date"]')[1].setValue('2026-01-31');
    await wrapper.findAll('button').find(b => b.text() === 'Apply')!.trigger('click');

    expect(vi.mocked(transactionService.getGrouped)).toHaveBeenCalled();
    const lastCall = vi.mocked(transactionService.getGrouped).mock.calls.at(-1)?.[0];
    expect(lastCall?.from).toBeTruthy();
    expect(lastCall?.to).toBeTruthy();
    expect(lastCall?.journalId).toBe('journal-1');
  });

  it('shows filter-aware empty state and can clear filters', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 0, net: 0 });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    await wrapper.find('input[type="date"]').setValue('2026-01-01');
    await wrapper.findAll('input[type="date"]')[1].setValue('2026-01-31');
    await wrapper.findAll('button').find(b => b.text() === 'Apply')!.trigger('click');
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('No transactions match the current filters.');
    const clearFiltersButton = wrapper.findAll('button').find(b => b.text() === 'Clear filters');
    expect(clearFiltersButton).toBeTruthy();
  });

  it('validates date range before filtering', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 0, net: 0 });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    await wrapper.find('input[type="date"]').setValue('2026-02-01');
    await wrapper.findAll('input[type="date"]')[1].setValue('2026-01-01');
    const before = vi.mocked(transactionService.getGrouped).mock.calls.length;
    await wrapper.findAll('button').find(b => b.text() === 'Apply')!.trigger('click');

    expect(wrapper.text()).toContain('The "From" date must be earlier than or equal to the "To" date.');
    expect(vi.mocked(transactionService.getGrouped).mock.calls.length).toBe(before);
  });

  it('creates a tag from manage tags section', async () => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 0, net: 0 });
    vi.mocked(transactionService.createTag).mockResolvedValue({ id: 't1', name: 'Food', color: '#6366f1' });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal }, attachTo: document.body });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    await wrapper.findAll('button').find(b => b.text().includes('Manage Tags'))!.trigger('click');
    await wrapper.vm.$nextTick();

    const tagInput = document.querySelector('input[placeholder="New tag name"]') as HTMLInputElement;
    tagInput.value = 'Food';
    tagInput.dispatchEvent(new Event('input'));
    await wrapper.vm.$nextTick();

    const addButton = Array.from(document.querySelectorAll('button')).find(b => b.textContent === 'Add');
    addButton?.click();
    await wrapper.vm.$nextTick();

    expect(vi.mocked(transactionService.createTag)).toHaveBeenCalledWith({ name: 'Food' });
    wrapper.unmount();
  });

  it('renders spending by tag section', async () => {
    const tx = { id: 's1', journalId: 'journal-1', journalName: 'My Budget', currency: 'USD', type: TransactionType.Out, amount: 50, occurredAt: new Date().toISOString(), note: 'Food', tags: [] };
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 0, totalOut: 50, net: -50 });
    vi.mocked(transactionService.getGrouped).mockResolvedValue([
      { year: new Date(tx.occurredAt).getFullYear(), rollups: [], months: [{ year: new Date(tx.occurredAt).getFullYear(), month: new Date(tx.occurredAt).getMonth() + 1, transactions: [tx], rollups: [] }] }
    ]);
    vi.mocked(transactionService.getSpendingByTag).mockResolvedValue({
      currency: 'USD',
      items: [{ tagId: 't1', tagName: 'Food', totalOut: 50 }]
    });

    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('Spending by Tag');
    expect(wrapper.text()).toContain('Food');
    expect(wrapper.text()).toContain('$50.00');
  });
});
