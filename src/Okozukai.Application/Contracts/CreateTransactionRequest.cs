using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Contracts;

public sealed record CreateTransactionRequest(
    Guid JournalId,
    TransactionType Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? Note,
    IReadOnlyCollection<Guid>? TagIds = null);
