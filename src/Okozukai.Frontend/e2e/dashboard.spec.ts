import { test, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5173';

test.describe('Okozukai E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    // Wait for initial load to finish
    await expect(page.getByText('Loading your budget...')).toBeHidden({ timeout: 15000 });
  });

  test('homepage loads without error banner', async ({ page }) => {
    // No error banner should appear
    await expect(page.getByText('Failed to load transactions. Is the API running?')).not.toBeVisible();
    // Balance card visible — component renders "USD Balance" (not "Balance (USD)")
    await expect(page.getByText(/USD Balance/)).toBeVisible();
    // Recent Transactions heading visible
    await expect(page.getByRole('heading', { name: 'Recent Transactions' })).toBeVisible();
  });

  test('create an income transaction', async ({ page }) => {
    const note = `E2E income ${Date.now()}`;

    await page.getByRole('button', { name: '+ New Transaction' }).click();
    await page.locator('form').getByRole('combobox').selectOption('Income (+)');
    await page.getByPlaceholder('0.00').fill('250');
    await page.getByRole('textbox', { name: 'What was this for?' }).fill(note);
    await page.getByRole('button', { name: 'Save' }).click();

    // Form closes and transaction appears
    await expect(page.getByRole('button', { name: '+ New Transaction' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(note)).toBeVisible();
    await expect(page.getByText('+ $250.00').first()).toBeVisible();
  });

  test('edit an existing transaction', async ({ page }) => {
    const note = `E2E edit ${Date.now()}`;

    // Create one to edit
    await page.getByRole('button', { name: '+ New Transaction' }).click();
    await page.getByPlaceholder('0.00').fill('10');
    await page.getByRole('textbox', { name: 'What was this for?' }).fill(note);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText(note)).toBeVisible({ timeout: 5000 });

    // Click Edit on the newly created transaction
    const row = page.getByText(note).first();
    const editBtn = row.locator('../..').getByRole('button', { name: 'Edit' });
    await editBtn.click();

    // Edit note
    const updatedNote = `${note} (edited)`;
    const noteInput = page.getByRole('textbox', { name: 'What was this for?' });
    await noteInput.clear();
    await noteInput.fill(updatedNote);
    await page.getByRole('button', { name: 'Update' }).click();

    // Updated note appears
    await expect(page.getByText(updatedNote)).toBeVisible({ timeout: 5000 });
  });

  test('delete a transaction', async ({ page }) => {
    const note = `E2E delete ${Date.now()}`;

    // Create one to delete
    await page.getByRole('button', { name: '+ New Transaction' }).click();
    await page.getByPlaceholder('0.00').fill('1');
    await page.getByRole('textbox', { name: 'What was this for?' }).fill(note);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText(note)).toBeVisible({ timeout: 5000 });

    // Delete it
    const row = page.getByText(note).first();
    const deleteBtn = row.locator('../..').getByRole('button', { name: 'Delete' });
    page.once('dialog', dialog => dialog.accept());
    await deleteBtn.click();

    // Transaction gone
    await expect(page.getByText(note)).not.toBeVisible({ timeout: 5000 });
  });

  test('create and use a tag', async ({ page }) => {
    const tagName = `Tag-${Date.now()}`;

    // Create tag — open manage tags modal first
    await page.getByRole('button', { name: 'Manage Tags' }).click();
    await page.getByRole('textbox', { name: 'New tag name' }).fill(tagName);
    await page.getByRole('button', { name: 'Add' }).click();

    // Tag appears in filter section and manage section
    await expect(page.getByText(tagName).first()).toBeVisible({ timeout: 5000 });

    // Create a transaction with that tag
    await page.getByRole('button', { name: '+ New Transaction' }).click();
    await page.getByPlaceholder('0.00').fill('75');
    await page.getByRole('textbox', { name: 'What was this for?' }).fill('Tagged expense');
    await page.locator('form').getByLabel(tagName).check();
    await page.getByRole('button', { name: 'Save' }).click();

    // Tag appears in filter section
    await expect(page.getByText(tagName).first()).toBeVisible({ timeout: 5000 });
  });

  test('year/month collapse toggle works', async ({ page }) => {
    // Find a year group and toggle it
    const yearButton = page.getByRole('button', { name: /^\d{4}/ }).first();
    await expect(yearButton).toBeVisible();
    await yearButton.click();
    // After collapse, year group content should be hidden
    // (month buttons inside disappear)
    const snapshot1 = await page.content();
    await yearButton.click();
    // After expand, content visible again
    await expect(page.getByRole('button', { name: /January|February|March|April|May|June|July|August|September|October|November|December/ }).first()).toBeVisible({ timeout: 3000 });
  });

  test('date filter restricts displayed transactions', async ({ page }) => {
    // Set from date to well in the future — should yield no results
    await page.locator('input[type="date"]').first().fill('2099-01-01');
    await page.locator('input[type="date"]').last().fill('2099-12-31');
    // Button text is "Apply" (not "Apply filters")
    await page.getByRole('button', { name: 'Apply' }).click();

    await expect(page.getByText('No transactions match the current filters.')).toBeVisible({ timeout: 5000 });

    // Clear filters restores transactions
    await page.getByRole('button', { name: 'Clear' }).click();
    await expect(page.getByText('No transactions match the current filters.')).not.toBeVisible({ timeout: 5000 });
  });
});

test.describe('Dashboard page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    await expect(page.getByText('Loading your budget...')).toBeHidden({ timeout: 15000 });
  });

  test('navigation tabs are visible and functional', async ({ page }) => {
    // Both tabs visible
    await expect(page.getByRole('link', { name: 'Transactions' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Dashboard' })).toBeVisible();

    // Transactions tab active by default
    const transactionsTab = page.getByRole('link', { name: 'Transactions' });
    await expect(transactionsTab).toHaveClass(/bg-indigo-600/);
  });

  test('navigates to dashboard page', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();

    // URL changes
    await expect(page).toHaveURL(/\/dashboard/);

    // Dashboard content loads — wait for loading to complete
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    // KPI summary card visible
    await expect(page.getByText(/USD Balance/)).toBeVisible({ timeout: 5000 });
  });

  test('dashboard page shows date preset buttons', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    await expect(page.getByRole('button', { name: 'This Month' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Last 3 Months' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Last 6 Months' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'This Year' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'All Time' })).toBeVisible();
  });

  test('date preset refetches data', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    // Click "This Month" preset
    await page.getByRole('button', { name: 'This Month' }).click();

    // Loading spinner briefly appears then disappears
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    // KPI card still present after refetch
    await expect(page.getByText(/USD Balance/)).toBeVisible();
  });

  test('customize panel toggles chart visibility', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    // Customize panel not visible by default
    await expect(page.getByText('Toggle Charts')).not.toBeVisible();

    // Open customize panel
    await page.getByRole('button', { name: /Customize/ }).click();
    await expect(page.getByText('Toggle Charts')).toBeVisible();

    // All 4 chart labels visible
    await expect(page.getByText('Monthly Income vs Expenses').first()).toBeVisible();
    await expect(page.getByText('Spending by Tag').first()).toBeVisible();
    await expect(page.getByText('Net Balance Trend')).toBeVisible();
    await expect(page.getByText('Monthly Spending by Tag').first()).toBeVisible();
  });

  test('hiding all charts shows empty state', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page.getByText('Loading dashboard...')).toBeHidden({ timeout: 10000 });

    // Open customize and uncheck all charts by clicking their labels
    await page.getByRole('button', { name: /Customize/ }).click();
    const labels = page.locator('label').filter({ has: page.locator('input[type="checkbox"]') });
    const count = await labels.count();
    for (let i = 0; i < count; i++) {
      await labels.nth(i).click();
    }

    await expect(page.getByText('All charts are hidden')).toBeVisible({ timeout: 3000 });
  });

  test('navigating back to transactions shows transaction list', async ({ page }) => {
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page).toHaveURL(/\/dashboard/);

    await page.getByRole('link', { name: 'Transactions' }).click();
    await expect(page).toHaveURL(/\/$/);

    await expect(page.getByRole('heading', { name: 'Recent Transactions' })).toBeVisible({ timeout: 5000 });
  });
});
