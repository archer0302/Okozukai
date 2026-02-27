using Microsoft.Extensions.Logging;
using Okozukai.Application.Contracts;
using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public sealed class JournalService
{
    private readonly IJournalRepository _journalRepository;
    private readonly ILogger<JournalService> _logger;

    public JournalService(IJournalRepository journalRepository, ILogger<JournalService> logger)
    {
        _journalRepository = journalRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<JournalResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var journals = await _journalRepository.GetAllAsync(ct);
        return journals.Select(JournalResponse.FromDomain).ToArray();
    }

    public async Task<JournalResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(id, ct);
        return journal is null ? null : JournalResponse.FromDomain(journal);
    }

    public async Task<JournalResponse> CreateAsync(CreateJournalRequest request, CancellationToken ct = default)
    {
        var journal = Journal.Create(request.Name, request.PrimaryCurrency);
        _journalRepository.Add(journal);
        await _journalRepository.SaveChangesAsync(ct);
        _logger.LogInformation("Created journal {JournalId} '{Name}' ({Currency}).", journal.Id, journal.Name, journal.PrimaryCurrency);
        return JournalResponse.FromDomain(journal);
    }

    public async Task<JournalResponse?> UpdateAsync(Guid id, UpdateJournalRequest request, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(id, ct);
        if (journal is null)
        {
            _logger.LogWarning("Attempted to update non-existent journal {JournalId}.", id);
            return null;
        }

        journal.Update(request.Name);
        _journalRepository.Update(journal);
        await _journalRepository.SaveChangesAsync(ct);
        _logger.LogInformation("Updated journal {JournalId}.", id);
        return JournalResponse.FromDomain(journal);
    }

    /// <summary>
    /// Deletes a journal. Only closed journals may be deleted; all their transactions are cascade-deleted by the DB.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(id, ct);
        if (journal is null)
        {
            _logger.LogWarning("Attempted to delete non-existent journal {JournalId}.", id);
            return false;
        }

        if (!journal.IsClosed)
        {
            throw new InvalidOperationException("Only closed journals can be deleted.");
        }

        _journalRepository.Delete(journal);
        await _journalRepository.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted journal {JournalId}.", id);
        return true;
    }

    public async Task<JournalResponse?> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(id, ct);
        if (journal is null)
        {
            return null;
        }

        journal.Close();
        _journalRepository.Update(journal);
        await _journalRepository.SaveChangesAsync(ct);
        _logger.LogInformation("Closed journal {JournalId}.", id);
        return JournalResponse.FromDomain(journal);
    }

    public async Task<JournalResponse?> ReopenAsync(Guid id, CancellationToken ct = default)
    {
        var journal = await _journalRepository.GetByIdAsync(id, ct);
        if (journal is null)
        {
            return null;
        }

        journal.Reopen();
        _journalRepository.Update(journal);
        await _journalRepository.SaveChangesAsync(ct);
        _logger.LogInformation("Reopened journal {JournalId}.", id);
        return JournalResponse.FromDomain(journal);
    }
}
