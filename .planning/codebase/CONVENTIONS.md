# Coding Conventions

**Analysis Date:** 2026-08-19

## Naming Patterns

### Files

**TypeScript/Vue:**
- camelCase for service files: `transactionService.ts`, `journalService.ts`
- PascalCase for component files: `TransactionDashboard.vue`, `DashboardPage.vue`
- camelCase for utility/type files: `client.ts`, `index.ts`
- `.spec.ts` suffix for unit tests
- `.spec.ts` for component tests (Vue Test Utils)
- `playwright.config.ts` for E2E test configuration

**C#:**
- PascalCase for all C# files matching class name
- Pattern: `{ClassName}.cs` (e.g., `Journal.cs`, `TransactionService.cs`)
- Suffix `.Designer.cs` for generated EF Core migration designer files
- Test files: `{ClassName}Tests.cs` (e.g., `TransactionServiceTests.cs`)

### Functions/Methods

**TypeScript:**
- camelCase for all functions and methods
- async methods use async/await pattern: `async getById(id: string)`
- Service methods as properties on object: `transactionService.getAll()`, `transactionService.create()`
- Factory/creator functions: `Create`, `CreateRequest`

**C#:**
- PascalCase for all public methods: `CreateAsync()`, `GetByIdAsync()`, `UpdateAsync()`
- Private methods: PascalCase (same convention as public)
- Async methods use `Async` suffix: `GetByIdAsync()`, `SaveChangesAsync()`
- Factory methods: `Create()` pattern returning new instances
- Validation methods: `Validate{Property}()` (e.g., `ValidateName()`)

### Variables

**TypeScript:**
- camelCase for all local variables and parameters
- `const` preferred over `let` or `var`
- Destructuring used for imports and object access
- Mock functions prefixed with `mock`: `mockJournal`, `mockResolvedValue`

**C#:**
- camelCase for local variables and parameters
- PascalCase for property names (public)
- Private fields use camelCase with underscore prefix: `_dbContext`, `_repositoryMock`, `_loggerMock`
- readonly keyword for immutable fields
- static readonly for constants: `private static readonly Guid TestJournalId`

### Types/Interfaces

**TypeScript:**
- PascalCase for interface names: `TransactionResponse`, `CreateTransactionRequest`
- Suffix `Response` for API response types
- Suffix `Request` for API request/input types
- Enums: PascalCase with PascalCase members: `enum TransactionType { In, Out }`
- Generic type parameters: Single letter or descriptive: `Promise<T>`, `IReadOnlyCollection<Guid>`

**C#:**
- PascalCase for interface names (prefix with `I`): `ITransactionRepository`, `ILogger<T>`
- PascalCase for record/class names
- Suffix `Response` for DTO response types: `TransactionResponse`, `JournalResponse`
- Suffix `Request` for input DTOs: `CreateTransactionRequest`, `UpdateJournalRequest`
- `sealed class` for domain entities to prevent inheritance
- Properties use auto-properties with private setters when appropriate

## Code Style

### Formatting

**TypeScript:**
- 4-space indentation (inferred from Vite config)
- No Prettier or ESLint config detected; follow project defaults
- Semicolons at end of statements (Vue TypeScript convention)
- Single quotes for strings (Vue convention, but not enforced)

**C#:**
- 4-space indentation (standard .NET)
- No BOM in files
- Namespace on single line: `namespace Okozukai.Domain.Transactions;`
- Opening braces on same line (Allman style):
  ```csharp
  if (condition)
  {
      // code
  }
  ```
- Async methods always use `async Task` or `async Task<T>`

### Linting

**TypeScript:**
- No ESLint or Prettier config found in project
- TypeScript strict mode enabled via `tsconfig.app.json`:
  - `"strict": true`
  - `"noUnusedLocals": true`
  - `"noUnusedParameters": true`
  - `"noFallthroughCasesInSwitch": true`
  - `"noUncheckedSideEffectImports": true`
- Enforces strong typing throughout frontend

**C#:**
- .NET language features enabled: nullable reference types (C# 11+)
- No explicit .editorconfig detected; uses project file defaults
- Compiler warnings treated as strict requirements
- Pragma warnings disabled only where documented: `#pragma warning disable CS8618` (with restore)

## Import Organization

**TypeScript Order:**
1. External library imports (vue, axios, etc.)
2. Type imports: `import type { Type } from '../types'`
3. Service/API imports: `import { transactionService } from '../api'`
4. Component imports
5. Utility imports

Example from `transactionService.ts`:
```typescript
import apiClient from './client';
import type {
    TransactionResponse,
    CreateTransactionRequest,
    // ...
} from '../types/transaction';
```

**C# Order:**
1. System imports
2. Microsoft imports
3. Application namespace imports (using statements)
4. Blank line before namespace declaration

Example from `TransactionServiceTests.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Moq;
using Okozukai.Application.Contracts;
using Okozukai.Application.Transactions;
using Okozukai.Domain.Transactions;

namespace Okozukai.UnitTests.Transactions;
```

## Path Aliases

**TypeScript:**
- No path aliases configured in `tsconfig.json`
- Use relative imports: `import { transactionService } from '../api/transactionService'`
- Structure follows directory hierarchy

**C#:**
- Uses full namespace paths, no aliases
- Assembly references via project dependencies
- Internal types accessed via namespace imports

## Error Handling

**TypeScript:**
- No explicit error handling patterns enforced in services (see `transactionService.ts`)
- Components/tests assume successful responses from API
- Error boundaries should be implemented at component level (not yet seen)
- Try/catch not used in provided examples; error handling delegated to caller

**C#:**
- Domain entities throw exceptions for validation failures
- `ArgumentException` for invalid inputs with descriptive messages
- `InvalidOperationException` for business logic violations (e.g., "Cannot create transaction in closed journal")
- `KeyNotFoundException` when entity not found (vs. returning null)
- Repository methods return `null` for not found cases when appropriate
- Service methods throw exceptions that propagate to API controllers
- Controllers map exceptions to HTTP status codes:
  - `InvalidOperationException` → HTTP 409 Conflict
  - `KeyNotFoundException` → HTTP 404 Not Found
  - `ArgumentException` → HTTP 400 Bad Request

Examples from tests:
```csharp
// Domain validation throws
await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(request));

// Repository returns null for not found
var result = await _sut.GetByIdAsync(id);
Assert.Null(result);
```

## Logging

**Framework:** Injected via Dependency Injection

**TypeScript (Frontend):**
- No logging framework detected
- Console methods available but not used in provided code
- Error states shown via UI (e.g., "Failed to load transactions. Is the API running?")

**C#:**
- `ILogger<T>` injected via DI container
- Structured logging via Microsoft.Extensions.Logging
- Injected into services: `private readonly Mock<ILogger<TransactionService>> _loggerMock`
- Pattern: Logger instance attached to service for internal logging

Example from `TransactionServiceTests.cs`:
```csharp
private readonly Mock<ILogger<TransactionService>> _loggerMock;
_sut = new TransactionService(
    _repositoryMock.Object,
    _journalRepositoryMock.Object,
    _tagRepositoryMock.Object,
    _loggerMock.Object);
```

## Comments

### When to Comment

**Required:**
- Pragma directives explaining suppressed warnings: `#pragma warning disable CS8618` with comment
- EF Core private constructors: `// Used by EF Core for entity materialization — bypasses domain validation.`
- Non-obvious business logic explaining why, not what

**Avoid:**
- Obvious comments restating code
- Large block comments (consider refactoring)
- Commented-out code (delete or explain via git history)

### JSDoc/TSDoc

**TypeScript:**
- Not observed in provided files
- Consider adding for public API methods and complex types
- Vue components should document props/events

**C#:**
- XML documentation comments (not observed in provided files)
- Consider `///` comments for public APIs and domain entities
- Entity properties should document constraints

## Function Design

### Size

**TypeScript:**
- Service methods: 5-10 lines typical (thin wrapper around API calls)
- Test cases: 10-20 lines including Arrange/Act/Assert
- Component methods: Varies by complexity; extract large handlers to separate functions

**C#:**
- Domain validation methods: 3-10 lines for guard clauses
- Repository methods: 5-15 lines for filtered queries
- Service methods: 10-20 lines orchestrating domain logic and persistence
- Test methods: 15-30 lines with Arrange/Act/Assert sections clearly separated

### Parameters

**TypeScript:**
- Prefer object parameters for multiple related values
- Example from `transactionService.ts`:
  ```typescript
  type FilterParams = { journalId: string; from?: string; to?: string; tagIds?: string[]; noteSearch?: string };
  async getAll(params: FilterParams & { page?: number; pageSize?: number })
  ```
- Optional parameters use `?` suffix

**C#:**
- Method signatures show explicit parameters
- Optional parameters use `= default` syntax
- CancellationToken as last parameter in async methods: `CancellationToken ct = default`
- Example from `TransactionRepository.cs`:
  ```csharp
  public async Task<IReadOnlyCollection<Transaction>> GetPagedAsync(
      Guid journalId,
      DateTimeOffset? from,
      DateTimeOffset? to,
      IReadOnlyCollection<Guid>? tagIds,
      int page,
      int pageSize,
      string? noteSearch = null,
      CancellationToken ct = default)
  ```

### Return Values

**TypeScript:**
- Return types explicitly typed with TypeScript generics
- Async methods return `Promise<T>`
- Services expose return values directly from API responses
- Example: `async getById(id: string): Promise<TransactionResponse>`

**C#:**
- Async methods return `Task<T>` or `Task` for void operations
- Collections return `IReadOnlyCollection<T>` for queries
- Nullable reference types indicate "may return null": `Task<Transaction?>`
- Methods return data directly or null; exceptions for errors
- Example: `public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)`

## Module Design

### Exports

**TypeScript:**
- Service objects exported as named const: `export const transactionService = { ... }`
- Types exported with `export interface` or `export enum`
- No default exports observed; prefer named exports
- All exports at module level

Example from `transactionService.ts`:
```typescript
export const transactionService = {
    async getAll(params: FilterParams & { page?: number; pageSize?: number }): Promise<TransactionResponse[]> { ... },
    async getById(id: string): Promise<TransactionResponse> { ... }
    // ...
};
```

**C#:**
- Public classes/interfaces exported from namespace
- Internal types use `internal` keyword for assembly-scoped visibility
- No export statement syntax; namespace qualification determines visibility
- Dependency injection for runtime service registration

Example DI from `DependencyInjection.cs`:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TransactionService>();
        services.AddScoped<TagService>();
        services.AddScoped<JournalService>();
        return services;
    }
}
```

### Barrel Files

**TypeScript:**
- `index.ts` acts as entry point: `src/main.ts`
- No barrel re-exports pattern observed in provided files
- Direct imports from specific files preferred

**C#:**
- No barrel file equivalent; use namespace organization
- Related types grouped in same namespace or directory

---

*Convention analysis: 2026-08-19*
