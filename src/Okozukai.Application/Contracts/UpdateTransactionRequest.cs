using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Contracts;

public sealed record UpdateTransactionRequest(
    TransactionType Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? Note,
    IReadOnlyCollection<Guid>? TagIds = null);
