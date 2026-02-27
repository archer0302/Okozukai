using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Contracts;

public sealed record TransactionResponse(
    Guid Id,
    Guid JournalId,
    string JournalName,
    string Currency,
    TransactionType Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? Note,
    IReadOnlyCollection<TagResponse> Tags)
{
    public static TransactionResponse FromDomain(Transaction transaction)
    {
        return new TransactionResponse(
            transaction.Id,
            transaction.JournalId,
            transaction.Journal?.Name ?? string.Empty,
            transaction.Journal?.PrimaryCurrency ?? string.Empty,
            transaction.Type,
            transaction.Amount,
            transaction.OccurredAt,
            transaction.Note,
            transaction.Tags.Select(TagResponse.FromDomain).ToArray());
    }
}
