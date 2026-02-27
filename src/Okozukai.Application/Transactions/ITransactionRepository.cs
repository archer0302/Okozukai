using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public interface ITransactionRepository
{
    Task<IReadOnlyCollection<Transaction>> GetPagedAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        int page,
        int pageSize,
        string? noteSearch = null,
        CancellationToken ct = default);
    Task<IReadOnlyCollection<TransactionCurrencySummary>> GetSummaryAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        string? noteSearch = null,
        CancellationToken ct = default);
    Task<IReadOnlyCollection<Transaction>> GetForGroupingAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        string? noteSearch = null,
        CancellationToken ct = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Transaction transaction);
    void Update(Transaction transaction);
    void Delete(Transaction transaction);
    Task SaveChangesAsync(CancellationToken ct = default);
}
