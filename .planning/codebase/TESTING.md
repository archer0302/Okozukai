# Testing Patterns

**Analysis Date:** 2026-08-19

## Test Framework

### TypeScript/Frontend

**Unit & Component Tests:**
- Framework: Vitest 4.0.18
- Test runner: vitest
- Config: `src/Okozukai.Frontend/vite.config.ts` (test section)
- Environment: jsdom (browser DOM simulation)
- Globals enabled: `globals: true` (no need to import describe/it/expect)

**E2E Tests:**
- Framework: Playwright 1.58.2
- Config: `src/Okozukai.Frontend/playwright.config.ts`
- Browsers: Chromium (Desktop Chrome)
- Base URL: Environment variable `BASE_URL` (defaults to http://localhost:5173)
- Parallel execution: `fullyParallel: false` (sequential tests)
- Workers: 1 (single worker)
- Retries: 0 in dev, 2 in CI
- Traces: On first retry for debugging

### C#/.NET Backend

**Unit Tests:**
- Framework: xUnit (via `[Fact]` attribute)
- Mocking: Moq for creating mock objects
- Config: No explicit config file; runs via dotnet test command
- Target: .NET 10

**Integration Tests:**
- Framework: xUnit with `IClassFixture<CustomWebApplicationFactory<Program>>`
- Factory: `CustomWebApplicationFactory<Program>` in `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs`
- HTTP Client: Injected via factory for making API calls
- Database: In-memory database or test database configured in factory

### Run Commands

```bash
# TypeScript unit tests
npm run test                 # Run all unit tests (Vitest)
npm run test -- --watch    # Watch mode (not in package.json, but available)

# TypeScript E2E tests
npm run test:e2e            # Run Playwright E2E tests

# C# unit tests
dotnet test tests/Okozukai.UnitTests

# C# integration tests
dotnet test tests/Okozukai.IntegrationTests

# All .NET tests
dotnet test
```

## Test File Organization

### TypeScript

**Location:**
- Unit/component tests: `src/tests/` directory (sibling to source)
- E2E tests: `e2e/` directory at root of frontend
- No co-located pattern (tests not in same directory as source files)

**Naming:**
- Component tests: `{ComponentName}.spec.ts` (e.g., `TransactionDashboard.spec.ts`)
- Service tests: `{ServiceName}.spec.ts` (e.g., `journalService.spec.ts`)

**Vite Configuration:**
```typescript
test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/tests/**/*.spec.ts']  // Only scans src/tests
}
```

### C#

**Location:**
- Unit tests: `tests/Okozukai.UnitTests/` project
- Integration tests: `tests/Okozukai.IntegrationTests/` project
- Organized by feature: `Okozukai.UnitTests/Transactions/TransactionServiceTests.cs`

**Naming:**
- Test classes: `{ClassName}Tests` (e.g., `TransactionServiceTests`, `JournalTests`)
- Test methods: `{MethodName}_{Condition}_{Expected}` (e.g., `CreateAsync_WithValidRequest_ReturnsResponseAndSaves`)

**Project Structure:**
```
tests/
├── Okozukai.UnitTests/
│   └── Transactions/
│       ├── TransactionServiceTests.cs
│       ├── TransactionTests.cs
│       └── JournalTests.cs
└── Okozukai.IntegrationTests/
    ├── CustomWebApplicationFactory.cs
    └── TransactionsApiTests.cs
```

## Test Structure

### TypeScript Test Suite Pattern

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';

describe('TransactionDashboard.vue', () => {
  beforeEach(() => {
    // Reset mocks before each test
    vi.mocked(transactionService.getGrouped).mockResolvedValue([]);
  });

  it('renders summary card with correct calculations', async () => {
    // Arrange - set up test data
    vi.mocked(transactionService.getSummary).mockResolvedValue({
      currency: 'USD',
      totalIn: 1000,
      totalOut: 250,
      net: 750
    });

    // Act - perform the test action
    const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
    await new Promise(resolve => setTimeout(resolve, 0));
    await wrapper.vm.$nextTick();

    // Assert - verify expectations
    const text = wrapper.text();
    expect(text).toContain('USD Balance');
    expect(text).toContain('$750.00');
  });
});
```

**Key Patterns:**
- `describe()` groups related tests
- `beforeEach()` runs setup before each test
- Arrange/Act/Assert structure (though not explicitly labeled)
- `vi.mock()` at module level for API services
- `vi.mocked()` to set return values
- `mount()` from @vue/test-utils for component rendering
- `wrapper.vm.$nextTick()` to wait for Vue reactivity
- `wrapper.text()` to get rendered text
- `wrapper.find()` / `wrapper.findAll()` for DOM queries

### C# Test Suite Pattern

```csharp
namespace Okozukai.UnitTests.Transactions;

public sealed class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _repositoryMock;
    private readonly Mock<IJournalRepository> _journalRepositoryMock;
    private readonly TransactionService _sut;  // System Under Test

    public TransactionServiceTests()
    {
        // Setup (constructor runs before each test via xUnit fixture)
        _repositoryMock = new Mock<ITransactionRepository>();
        _journalRepositoryMock = new Mock<IJournalRepository>();
        _sut = new TransactionService(_repositoryMock.Object, _journalRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsResponseAndSaves()
    {
        // Arrange
        var request = new CreateTransactionRequest(
            journalId: TestJournal.Id,
            type: TransactionType.In,
            amount: 100.50m,
            occurredAt: DateTimeOffset.UtcNow,
            note: "Initial deposit"
        );

        _repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTransaction);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Amount, result.Amount);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Once);
    }
}
```

**Key Patterns:**
- Constructor initializes mocks (runs before each test)
- `[Fact]` attribute marks test method
- SUT (System Under Test) stored in field: `_sut`
- Three clear sections: Arrange / Act / Assert
- Mocks set up with `Setup().ReturnsAsync()`
- `It.IsAny<T>()` for flexible argument matching
- `Verify()` checks methods were called correct number of times
- Test name format: `{Method}_{Condition}_{Expected}`

### Playwright E2E Test Pattern

```typescript
import { test, expect } from '@playwright/test';

test.describe('Okozukai E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate and wait for loading
    await page.goto(BASE_URL);
    await expect(page.getByText('Loading your budget...')).toBeHidden({ timeout: 15000 });
  });

  test('create an income transaction', async ({ page }) => {
    // Arrange (via beforeEach page setup)
    const note = `E2E income ${Date.now()}`;

    // Act
    await page.getByRole('button', { name: '+ New Transaction' }).click();
    await page.getByPlaceholder('0.00').fill('250');
    await page.getByRole('textbox', { name: 'What was this for?' }).fill(note);
    await page.getByRole('button', { name: 'Save' }).click();

    // Assert
    await expect(page.getByText(note)).toBeVisible();
    await expect(page.getByText('+ $250.00').first()).toBeVisible();
  });
});
```

**Key Patterns:**
- `test.beforeEach()` runs before each test
- Page fixture injected: `async ({ page })`
- Role-based selectors: `getByRole('button', { name: '...' })`
- Placeholder-based selectors: `getByPlaceholder('0.00')`
- Text-based selectors: `getByText()`
- Chain calls for navigation/interaction
- Timeouts for async operations: `{ timeout: 15000 }`
- Unique identifiers via timestamp: `Date.now()`

## Mocking

### TypeScript (Vitest)

**Mock Framework:** Vitest built-in `vi` utilities

**Service Mocking Pattern:**
```typescript
// Mock at module level
vi.mock('../api/transactionService', () => ({
  transactionService: {
    getAll: vi.fn(),
    getSummary: vi.fn(),
    getGrouped: vi.fn(),
    getTags: vi.fn(),
    createTag: vi.fn(),
    deleteTag: vi.fn(),
    delete: vi.fn()
  }
}));

// Set return values in tests
vi.mocked(transactionService.getSummary).mockResolvedValue({
  currency: 'USD',
  totalIn: 100,
  totalOut: 0,
  net: 100
});

// Verify calls
expect(vi.mocked(transactionService.getGrouped)).toHaveBeenCalled();
const lastCall = vi.mocked(transactionService.getGrouped).mock.calls.at(-1)?.[0];
```

**What to Mock:**
- External API services (transactionService, journalService)
- HTTP clients
- Other feature services that your component depends on

**What NOT to Mock:**
- DOM elements (use @vue/test-utils mount/wrapper)
- Vue's reactivity system
- Browser APIs that jsdom provides

### C# (Moq)

**Mock Framework:** Moq

**Setup Pattern:**
```csharp
private readonly Mock<ITransactionRepository> _repositoryMock;

public TransactionServiceTests()
{
    _repositoryMock = new Mock<ITransactionRepository>();
    // Setup default behavior
    _repositoryMock
        .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Transaction?)null);
}

// Override in specific tests
_repositoryMock
    .Setup(x => x.GetByIdAsync(testId, It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedTransaction);
```

**Verification Pattern:**
```csharp
// Verify called exactly once
_repositoryMock.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Once);

// Verify called at least once
_repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

// Verify never called
_repositoryMock.Verify(x => x.Delete(It.IsAny<Transaction>()), Times.Never);
```

**What to Mock:**
- Repository interfaces (ITransactionRepository, IJournalRepository)
- Logger (ILogger<T>)
- External services that have I* interface

**What NOT to Mock:**
- Domain entities (use Create() factory methods)
- Value objects
- Records/DTOs

## Fixtures and Factories

### TypeScript Test Data

**Mock Objects Pattern:**
```typescript
const mockJournal: JournalResponse = {
  id: 'journal-1',
  name: 'My Budget',
  primaryCurrency: 'USD',
  isClosed: false,
  createdAt: new Date().toISOString()
};

const mockTransaction: TransactionResponse = {
  id: '1',
  journalId: 'journal-1',
  journalName: 'My Budget',
  currency: 'USD',
  type: TransactionType.In,
  amount: 100,
  occurredAt: new Date().toISOString(),
  note: 'Salary',
  tags: []
};
```

**Location:**
- Mock data defined at top of test file (not in separate fixtures directory)
- Reused across multiple tests via beforeEach

**Pattern:**
- Immutable objects created fresh per test
- Use `new Date()` for timestamps (fresh each test)
- Factory pattern not observed; direct object construction

### C# Test Data

**Static Test Data:**
```csharp
private static readonly Guid TestJournalId = Guid.NewGuid();
private static readonly Journal TestJournal = Journal.Create("Test Journal", "USD");

public TransactionServiceTests()
{
    // Reused across all tests in class
}
```

**Factory Methods:**
```csharp
[Fact]
public async Task CreateAsync_WithValidRequest_ReturnsResponseAndSaves()
{
    // Arrange - create test domain objects
    var request = new CreateTransactionRequest(
        TestJournal.Id,
        TransactionType.In,
        100.50m,
        DateTimeOffset.UtcNow,
        "Initial deposit"
    );

    var createdTransaction = Transaction.Create(
        TestJournal.Id,
        false,
        request.Type,
        request.Amount,
        request.OccurredAt,
        request.Note
    );
}
```

**Location:**
- Static fields at class level for reused data
- Local variables in test methods for test-specific setup
- No separate fixtures directory

**Pattern:**
- Domain entity factories (`Journal.Create()`, `Transaction.Create()`)
- Request DTOs created inline
- `DateTimeOffset.UtcNow` for current timestamps in tests

## Coverage

**Requirements:**
- No explicit coverage requirements enforced by project config
- Coverage tracking not observed in build pipeline
- Tests exist for critical business logic (Transactions, Journals, Tags)

### View Coverage

**TypeScript:**
```bash
npm run test -- --coverage    # Generate coverage report (not in package.json)
# Vitest outputs to console; use coverage configuration in vite.config.ts
```

**C#:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
# Generates coverage reports for analysis tools
```

## Test Types

### Unit Tests

**TypeScript:**
- **Scope:** Individual service methods, component logic
- **Example:** `TransactionDashboard.spec.ts` - mocked API service, testing component rendering and user interactions
- **Approach:** Vitest with jsdom environment
- **Location:** `src/Okozukai.Frontend/src/tests/`
- **Files:**
  - `src/tests/TransactionDashboard.spec.ts` - 187 lines covering 9 test cases
  - `src/tests/DashboardPage.spec.ts` - Component tests

**C#:**
- **Scope:** Individual service methods, domain entity logic
- **Example:** `TransactionServiceTests.cs` - mocked repositories, testing create/read/update/delete
- **Approach:** xUnit with Moq
- **Location:** `tests/Okozukai.UnitTests/Transactions/`
- **Files:**
  - `TransactionServiceTests.cs` - 299 lines, 13+ test cases
  - `JournalTests.cs` - Domain entity tests
  - `TransactionTests.cs` - Domain entity tests

### Integration Tests

**TypeScript:**
- **Framework:** Playwright E2E
- **Scope:** Full application stack (frontend + backend API)
- **Example:** `e2e/dashboard.spec.ts` - creates real transactions, verifies UI updates
- **Approach:** Browser automation with real HTTP requests
- **Location:** `src/Okozukai.Frontend/e2e/`
- **Files:**
  - `e2e/dashboard.spec.ts` - 229 lines covering ~15 E2E scenarios
  - Tests cover: CRUD operations, filtering, tagging, validation

**C#:**
- **Framework:** xUnit with `IClassFixture<CustomWebApplicationFactory<Program>>`
- **Scope:** API endpoints with real database and business logic
- **Example:** `TransactionsApiTests.cs` - creates journals/transactions via HTTP, verifies responses
- **Approach:** Test server (WebApplicationFactory) with in-memory or test database
- **Location:** `tests/Okozukai.IntegrationTests/`
- **Files:**
  - `TransactionsApiTests.cs` - 407 lines covering ~35 test cases
  - Tests cover: CRUD, paging, filtering, validation, error cases

### E2E Tests

**Playwright Configuration:**
```typescript
// src/Okozukai.Frontend/playwright.config.ts
export default defineConfig({
  testDir: './e2e',           // Only runs files in e2e/
  fullyParallel: false,       // Sequential (not parallel)
  forbidOnly: !!process.env.CI, // Forbid .only in CI
  retries: process.env.CI ? 2 : 0, // Retry failures in CI
  workers: 1,                 // Single worker
  reporter: 'list',           // Console output
  use: {
    baseURL: process.env.BASE_URL,
    trace: 'on-first-retry'   // Trace failures
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ]
});
```

**Test Organization (from `dashboard.spec.ts`):**
- Uses `test.describe()` to group related scenarios
- Uses `test.beforeEach()` for common setup (navigate, wait for load)
- Tests independent user workflows (create, edit, delete, filter, tag)
- Unique IDs via `Date.now()` to avoid conflicts between parallel runs
- Timeouts for async operations (5-15 seconds typical)

## Common Patterns

### Async Testing in TypeScript

```typescript
// Wait for Vue reactivity
await wrapper.vm.$nextTick();

// Wait for promises
await new Promise(resolve => setTimeout(resolve, 0));

// Set mock return value and test
vi.mocked(transactionService.getSummary).mockResolvedValue({
  currency: 'USD',
  totalIn: 1000,
  totalOut: 250,
  net: 750
});

// Playwright waits for conditions
await expect(page.getByText('No transactions')).toBeVisible();
await expect(page.getByText('Loading...')).toBeHidden({ timeout: 15000 });
```

### Async Testing in C#

```csharp
[Fact]
public async Task CreateAsync_WithValidRequest_ReturnsResponseAndSaves()
{
    // Setup async mock
    _repositoryMock
        .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(createdTransaction);

    // Act - await async method
    var result = await _sut.CreateAsync(request);

    // Assert
    Assert.NotNull(result);
}
```

### Error Testing in TypeScript

```typescript
// Test error state via mock
vi.mocked(transactionService.getSummary).mockResolvedValue({
  currency: 'USD',
  totalIn: 0,
  totalOut: 0,
  net: 0
});

const wrapper = mount(TransactionDashboard, { props: { journal: mockJournal } });
await new Promise(resolve => setTimeout(resolve, 0));
await wrapper.vm.$nextTick();

// Verify empty state message
expect(wrapper.text()).toContain('No transactions recorded yet');
```

### Error Testing in C#

```csharp
[Fact]
public async Task CreateAsync_WhenJournalIsClosed_ThrowsInvalidOperationException()
{
    // Arrange - create closed journal
    var closedJournal = Journal.Create("Closed", "USD");
    closedJournal.Close();
    _journalRepositoryMock
        .Setup(x => x.GetByIdAsync(closedJournal.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(closedJournal);

    var request = new CreateTransactionRequest(
        closedJournal.Id,
        TransactionType.In,
        100m,
        DateTimeOffset.UtcNow,
        null
    );

    // Act & Assert - expect exception
    await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(request));
    _repositoryMock.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Never);
}
```

### Validation Testing in Playwright

```typescript
test('validates date range before filtering', async ({ page }) => {
  // Set invalid date range (from > to)
  await page.find('input[type="date"]').setValue('2026-02-01');
  await page.findAll('input[type="date"]')[1].setValue('2026-01-01');
  
  // Click apply
  const before = vi.mocked(transactionService.getGrouped).mock.calls.length;
  await page.getByRole('button', { name: 'Apply' }).click();

  // Verify error message shown and API not called
  expect(wrapper.text()).toContain('The "From" date must be earlier than or equal to the "To" date.');
  expect(vi.mocked(transactionService.getGrouped).mock.calls.length).toBe(before);
});
```

---

*Testing analysis: 2026-08-19*
