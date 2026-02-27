import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import DashboardPage from '../components/dashboard/DashboardPage.vue';
import { transactionService } from '../api/transactionService';
import type { JournalResponse } from '../types/transaction';

const mockJournal: JournalResponse = {
  id: 'journal-1',
  name: 'My Budget',
  primaryCurrency: 'USD',
  isClosed: false,
  createdAt: new Date().toISOString(),
};

vi.mock('../api/transactionService', () => ({
  transactionService: {
    getSummary: vi.fn(),
    getGrouped: vi.fn(),
    getSpendingByTag: vi.fn(),
    getSpendingByTagMonthly: vi.fn(),
    getTags: vi.fn(),
  },
}));

// Mock canvas for Chart.js in jsdom
HTMLCanvasElement.prototype.getContext = (() => null) as any;

describe('DashboardPage.vue', () => {
  beforeEach(() => {
    vi.mocked(transactionService.getSummary).mockResolvedValue({ currency: 'USD', totalIn: 500, totalOut: 200, net: 300 });
    vi.mocked(transactionService.getGrouped).mockResolvedValue([]);
    vi.mocked(transactionService.getSpendingByTag).mockResolvedValue({ currency: 'USD', items: [] });
    vi.mocked(transactionService.getSpendingByTagMonthly).mockResolvedValue({ currency: 'USD', months: [] });
    vi.mocked(transactionService.getTags).mockResolvedValue([]);
  });

  it('renders summary KPI cards', async () => {
    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('USD Balance');
    expect(wrapper.text()).toContain('$300.00');
    expect(wrapper.text()).toContain('$500.00');
    expect(wrapper.text()).toContain('$200.00');
  });

  it('renders date preset buttons', async () => {
    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('This Month');
    expect(wrapper.text()).toContain('Last 3 Months');
    expect(wrapper.text()).toContain('All Time');
  });

  it('switches date preset and re-fetches data', async () => {
    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    const callsBefore = vi.mocked(transactionService.getGrouped).mock.calls.length;

    const thisMonthBtn = wrapper.findAll('button').find(b => b.text() === 'This Month');
    await thisMonthBtn!.trigger('click');
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(vi.mocked(transactionService.getGrouped).mock.calls.length).toBeGreaterThan(callsBefore);
    const lastCall = vi.mocked(transactionService.getGrouped).mock.calls.at(-1)?.[0] as any;
    expect(lastCall.from).toBeTruthy();
  });

  it('shows empty chart messages when no data', async () => {
    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('No transaction data available for chart');
    expect(wrapper.text()).toContain('No spending data available for chart');
  });

  it('toggles customize panel and can hide charts', async () => {
    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    // Customize panel should not be visible initially
    expect(wrapper.text()).not.toContain('Toggle Charts');

    // Click customize button
    const customizeBtn = wrapper.findAll('button').find(b => b.text().includes('Customize'));
    await customizeBtn!.trigger('click');
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('Toggle Charts');
    expect(wrapper.text()).toContain('Monthly Income vs Expenses');
    expect(wrapper.text()).toContain('Spending by Tag');
  });

  it('shows empty state when all charts are hidden', async () => {
    // Pre-seed localStorage with all charts hidden
    localStorage.setItem(`dashboard-widgets-${mockJournal.id}`, JSON.stringify({
      monthlyBar: false, spendingPie: false, balanceTrend: false, monthlyTagStacked: false
    }));

    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('All charts are hidden');
    localStorage.removeItem(`dashboard-widgets-${mockJournal.id}`);
  });

  it('shows error state when API fails', async () => {
    vi.mocked(transactionService.getSummary).mockRejectedValue(new Error('Network error'));
    vi.mocked(transactionService.getGrouped).mockRejectedValue(new Error('Network error'));
    vi.mocked(transactionService.getSpendingByTag).mockRejectedValue(new Error('Network error'));
    vi.mocked(transactionService.getSpendingByTagMonthly).mockRejectedValue(new Error('Network error'));
    vi.mocked(transactionService.getTags).mockRejectedValue(new Error('Network error'));

    const wrapper = mount(DashboardPage, { props: { journal: mockJournal } });
    await new Promise(r => setTimeout(r, 0));
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('Failed to load dashboard data');
  });
});
