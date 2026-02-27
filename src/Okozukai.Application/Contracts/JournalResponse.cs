using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Contracts;

public sealed record JournalResponse(
    Guid Id,
    string Name,
    string PrimaryCurrency,
    bool IsClosed,
    DateTimeOffset CreatedAt)
{
    public static JournalResponse FromDomain(Journal journal)
    {
        return new JournalResponse(
            journal.Id,
            journal.Name,
            journal.PrimaryCurrency,
            journal.IsClosed,
            journal.CreatedAt);
    }
}
