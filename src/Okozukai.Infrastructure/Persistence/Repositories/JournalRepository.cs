using Microsoft.EntityFrameworkCore;
using Okozukai.Application.Transactions;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Repositories;

public sealed class JournalRepository : IJournalRepository
{
    private readonly OkozukaiDbContext _dbContext;

    public JournalRepository(OkozukaiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Journal>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Journals
            .OrderBy(j => j.CreatedAt)
            .ToArrayAsync(ct);
    }

    public async Task<Journal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Journals.FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public void Add(Journal journal)
    {
        _dbContext.Journals.Add(journal);
    }

    public void Update(Journal journal)
    {
        _dbContext.Journals.Update(journal);
    }

    public void Delete(Journal journal)
    {
        _dbContext.Journals.Remove(journal);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
