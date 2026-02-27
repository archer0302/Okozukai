using Microsoft.Extensions.Logging;
using System.Text;
using Okozukai.Application.Contracts;
using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public sealed class TransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IJournalRepository _journalRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IJournalRepository journalRepository,
        ITagRepository tagRepository,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _journalRepository = journalRepository;
        _tagRepository = tagRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TransactionResponse>> GetAllAsync(
        Guid journalId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        int page = 1,
        int pageSize = 50,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize switch
        {
            <= 0 => 50,
            > 200 => 200,
            _ => pageSize
        };

        var paged = await _transactionRepository.GetPagedAsync(journalId, from, to, tagIds, page, pageSize, noteSearch, ct);
        return paged.Select(TransactionResponse.FromDomain).ToArray();
    }

    public async Task<TransactionSummaryResponse> GetSummaryAsync(
        Guid journalId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var summaries = await _transactionRepository.GetSummaryAsync(journalId, from, to, tagIds, noteSearch, ct);
        var summary = summaries.FirstOrDefault();
        return summary is not null
            ? new TransactionSummaryResponse(summary.Currency, summary.TotalIn, summary.TotalOut, summary.TotalIn - summary.TotalOut)
            : new TransactionSummaryResponse(string.Empty, 0m, 0m, 0m);
    }

    public async Task<IReadOnlyCollection<TransactionYearGroupResponse>> GetGroupedAsync(
        Guid journalId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var transactions = await _transactionRepository.GetForGroupingAsync(journalId, from, to, tagIds, noteSearch, ct);
        _logger.LogInformation("Building grouped transactions for {Count} records.", transactions.Count);

        return transactions
            .GroupBy(x => x.OccurredAt.Year)
            .OrderByDescending(x => x.Key)
            .Select(yearGroup =>
            {
                var months = yearGroup
                    .GroupBy(x => x.OccurredAt.Month)
                    .OrderByDescending(x => x.Key)
                    .Select(monthGroup =>
                    {
                        var monthTransactions = monthGroup
                            .OrderByDescending(x => x.OccurredAt)
                            .Select(TransactionResponse.FromDomain)
                            .ToArray();

                        return new TransactionMonthGroupResponse(
                            yearGroup.Key,
                            monthGroup.Key,
                            monthTransactions,
                            BuildRollups(monthGroup));
                    })
                    .ToArray();

                return new TransactionYearGroupResponse(
                    yearGroup.Key,
                    months,
                    BuildRollups(yearGroup));
            })
            .ToArray();
    }

    public async Task<TransactionSpendingByTagResponse> GetSpendingByTagAsync(
        Guid journalId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var transactions = await _transactionRepository.GetForGroupingAsync(journalId, from, to, tagIds, noteSearch, ct);
        _logger.LogInformation("Building spending-by-tag breakdown for {Count} records.", transactions.Count);

        var tagMap = new Dictionary<(Guid? TagId, string TagName), decimal>();

        foreach (var transaction in transactions)
        {
            var spendingAmount = transaction.Type == TransactionType.Out ? transaction.Amount : 0m;
            if (spendingAmount <= 0)
            {
                continue;
            }

            if (transaction.Tags.Count == 0)
            {
                var key = ((Guid?)null, "Untagged");
                tagMap[key] = tagMap.GetValueOrDefault(key) + spendingAmount;
                continue;
            }

            var splitAmount = spendingAmount / transaction.Tags.Count;
            foreach (var tag in transaction.Tags)
            {
                var key = ((Guid?)tag.Id, tag.Name);
                tagMap[key] = tagMap.GetValueOrDefault(key) + splitAmount;
            }
        }

        // Since each journal has a single currency, return a single-element list.
        var journal = await _journalRepository.GetByIdAsync(journalId, ct);
        var currency = journal?.PrimaryCurrency ?? string.Empty;

        return new TransactionSpendingByTagResponse(
                currency,
                tagMap
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key.TagName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new TransactionSpendingByTagItemResponse(x.Key.TagId, x.Key.TagName, decimal.Round(x.Value, 2)))
                    .ToArray());
    }

    public async Task<SpendingByTagMonthlyResponse> GetSpendingByTagMonthlyAsync(
        Guid journalId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        string? noteSearch = null,
        CancellationToken ct = default)
    {
        var transactions = await _transactionRepository.GetForGroupingAsync(journalId, from, to, tagIds, noteSearch, ct);
        _logger.LogInformation("Building monthly spending-by-tag breakdown for {Count} records.", transactions.Count);

        var journal = await _journalRepository.GetByIdAsync(journalId, ct);
        var currency = journal?.PrimaryCurrency ?? string.Empty;

        var months = transactions
            .Where(t => t.Type == TransactionType.Out)
            .GroupBy(t => (t.OccurredAt.Year, t.OccurredAt.Month))
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(monthGroup =>
            {
                var tagMap = new Dictionary<(Guid? TagId, string TagName), decimal>();

                foreach (var transaction in monthGroup)
                {
                    if (transaction.Tags.Count == 0)
                    {
                        var key = ((Guid?)null, "Untagged");
                        tagMap[key] = tagMap.GetValueOrDefault(key) + transaction.Amount;
                        continue;
                    }

                    var splitAmount = transaction.Amount / transaction.Tags.Count;
                    foreach (var tag in transaction.Tags)
                    {
                        var key = ((Guid?)tag.Id, tag.Name);
                        tagMap[key] = tagMap.GetValueOrDefault(key) + splitAmount;
                    }
                }

                return new SpendingByTagMonthResponse(
                    monthGroup.Key.Year,
                    monthGroup.Key.Month,
                    tagMap
                        .OrderByDescending(x => x.Value)
                        .ThenBy(x => x.Key.TagName, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new TransactionSpendingByTagItemResponse(x.Key.TagId, x.Key.TagName, decimal.Round(x.Value, 2)))
                        .ToArray());
            })
            .ToArray();

        return new SpendingByTagMonthlyResponse(currency, months);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        Guid journalId,
        string format,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        CancellationToken ct = default)
    {
        var normalized = format.Trim().ToLowerInvariant();
        var transactions = await _transactionRepository.GetForGroupingAsync(journalId, from, to, tagIds, null, ct);
        var responses = transactions.Select(TransactionResponse.FromDomain).ToArray();

        if (normalized == "json")
        {
            var payload = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(responses));
            _logger.LogInformation("Exported {Count} transactions as JSON.", responses.Length);
            return (payload, "application/json", "transactions.json");
        }

        if (normalized == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("Id,JournalId,JournalName,Currency,Type,Amount,OccurredAt,Note,Tags");
            foreach (var item in responses)
            {
                var tags = string.Join('|', item.Tags.Select(x => x.Name));
                csv.AppendLine(string.Join(",",
                    EscapeCsv(item.Id.ToString()),
                    EscapeCsv(item.JournalId.ToString()),
                    EscapeCsv(item.JournalName),
                    EscapeCsv(item.Currency),
                    EscapeCsv(item.Type.ToString()),
                    EscapeCsv(item.Amount.ToString()),
                    EscapeCsv(item.OccurredAt.ToString("O")),
                    EscapeCsv(item.Note),
                    EscapeCsv(tags)));
            }

            _logger.LogInformation("Exported {Count} transactions as CSV.", responses.Length);
            return (Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "transactions.csv");
        }

        throw new ArgumentException("Unsupported export format. Use 'json' or 'csv'.", nameof(format));
    }

    public async Task<TransactionResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, ct);
        return transaction is null ? null : TransactionResponse.FromDomain(transaction);
    }

    public async Task<TransactionResponse> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(request.JournalId, ct)
            ?? throw new KeyNotFoundException($"Journal {request.JournalId} not found.");

        var transaction = Transaction.Create(
            journal.Id,
            journal.IsClosed,
            request.Type,
            request.Amount,
            request.OccurredAt,
            request.Note);

        var tags = await ResolveTagsAsync(request.TagIds, ct);
        transaction.SetTags(tags);

        _transactionRepository.Add(transaction);
        await _transactionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Created transaction {TransactionId} of type {Type} with amount {Amount} in journal {JournalId}.",
            transaction.Id, transaction.Type, transaction.Amount, journal.Id);

        // Re-fetch so Journal nav property is populated for the response.
        var created = await _transactionRepository.GetByIdAsync(transaction.Id, ct);
        return TransactionResponse.FromDomain(created!);
    }

    public async Task<TransactionResponse?> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, ct);
        if (transaction is null)
        {
            _logger.LogWarning("Attempted to update non-existent transaction {TransactionId}.", id);
            return null;
        }

        var journal = await _journalRepository.GetByIdAsync(transaction.JournalId, ct)
            ?? throw new KeyNotFoundException($"Journal {transaction.JournalId} not found.");

        transaction.Update(
            journal.IsClosed,
            request.Type,
            request.Amount,
            request.OccurredAt,
            request.Note);

        var tags = await ResolveTagsAsync(request.TagIds, ct);
        transaction.SetTags(tags);

        _transactionRepository.Update(transaction);
        await _transactionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Updated transaction {TransactionId}.", transaction.Id);

        var updated = await _transactionRepository.GetByIdAsync(transaction.Id, ct);
        return TransactionResponse.FromDomain(updated!);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, ct);
        if (transaction is null)
        {
            _logger.LogWarning("Attempted to delete non-existent transaction {TransactionId}.", id);
            return false;
        }

        var journal = await _journalRepository.GetByIdAsync(transaction.JournalId, ct)
            ?? throw new KeyNotFoundException($"Journal {transaction.JournalId} not found.");

        if (journal.IsClosed)
        {
            throw new InvalidOperationException("Cannot delete a transaction from a closed journal.");
        }

        _transactionRepository.Delete(transaction);
        await _transactionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted transaction {TransactionId}.", id);

        return true;
    }

    private async Task<IReadOnlyCollection<Tag>> ResolveTagsAsync(IReadOnlyCollection<Guid>? tagIds, CancellationToken ct)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return [];
        }

        var distinctIds = tagIds.Distinct().ToArray();
        var tags = await _tagRepository.GetByIdsAsync(distinctIds, ct);
        if (tags.Count != distinctIds.Length)
        {
            throw new ArgumentException("One or more tag IDs are invalid.", nameof(tagIds));
        }

        return tags;
    }

    private static IReadOnlyCollection<TransactionPeriodRollupResponse> BuildRollups(IEnumerable<Transaction> transactions)
    {
        // Since all transactions in a journal share the same currency, we only have one rollup entry.
        var list = transactions.ToList();
        var currency = list.FirstOrDefault()?.Journal?.PrimaryCurrency ?? string.Empty;
        var totalIn = list.Where(t => t.Type == TransactionType.In).Sum(t => t.Amount);
        var totalOut = list.Where(t => t.Type == TransactionType.Out).Sum(t => t.Amount);
        var net = totalIn - totalOut;
        if (!list.Any())
        {
            return [];
        }

        return [new TransactionPeriodRollupResponse(currency, 0m, totalIn, totalOut, net, net)];
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
