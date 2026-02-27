using Microsoft.EntityFrameworkCore;
using Okozukai.Application.Transactions;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly OkozukaiDbContext _dbContext;

    public TransactionRepository(OkozukaiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Transaction>> GetPagedAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        int page,
        int pageSize,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(journalId, from, to, tagIds, noteSearch);

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<TransactionCurrencySummary>> GetSummaryAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(journalId, from, to, tagIds, noteSearch);

        var inflowEntries = query
            .Where(x => x.Type == TransactionType.In)
            .Select(x => new { x.Journal!.PrimaryCurrency, TotalIn = x.Amount, TotalOut = 0m });

        var outflowEntries = query
            .Where(x => x.Type == TransactionType.Out)
            .Select(x => new { x.Journal!.PrimaryCurrency, TotalIn = 0m, TotalOut = x.Amount });

        return await inflowEntries
            .Concat(outflowEntries)
            .GroupBy(x => x.PrimaryCurrency)
            .Select(g => new TransactionCurrencySummary(
                g.Key,
                g.Sum(x => x.TotalIn),
                g.Sum(x => x.TotalOut)))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<Transaction>> GetForGroupingAsync(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(journalId, from, to, tagIds, noteSearch);
        return await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Transactions
            .Include(x => x.Tags)
            .Include(x => x.Journal)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public void Add(Transaction transaction)
    {
        _dbContext.Transactions.Add(transaction);
    }

    public void Update(Transaction transaction)
    {
        _dbContext.Transactions.Update(transaction);
    }

    public void Delete(Transaction transaction)
    {
        _dbContext.Transactions.Remove(transaction);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }

    private IQueryable<Transaction> BuildFilteredQuery(
        Guid journalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid>? tagIds,
        string? noteSearch = null)
    {
        var query = _dbContext.Transactions
            .Include(x => x.Tags)
            .Include(x => x.Journal)
            .Where(x => x.JournalId == journalId)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        if (tagIds is { Count: > 0 })
        {
            query = query.Where(x => x.Tags.Any(t => tagIds.Contains(t.Id)));
        }

        if (!string.IsNullOrWhiteSpace(noteSearch))
        {
            var escaped = noteSearch.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(x => x.Note != null && EF.Functions.ILike(x.Note, $"%{escaped}%"));
        }

        return query;
    }
}
