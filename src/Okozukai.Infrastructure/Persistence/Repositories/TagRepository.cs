using Microsoft.EntityFrameworkCore;
using Okozukai.Application.Transactions;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Repositories;

public sealed class TagRepository : ITagRepository
{
    private readonly OkozukaiDbContext _dbContext;

    public TagRepository(OkozukaiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Tag>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .OrderBy(x => x.Name)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<Tag>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Tags
            .Where(x => ids.Contains(x.Id))
            .ToArrayAsync(ct);
    }

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Tags.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Tag?> GetByNameAsync(string normalizedName, CancellationToken ct = default)
    {
        return await _dbContext.Tags.FirstOrDefaultAsync(x => x.Name == normalizedName, ct);
    }

    public async Task DetachFromTransactionsAsync(Guid tagId, CancellationToken ct = default)
    {
        var transactions = await _dbContext.Transactions
            .Include(x => x.Tags)
            .Where(x => x.Tags.Any(t => t.Id == tagId))
            .ToArrayAsync(ct);

        foreach (var transaction in transactions)
        {
            transaction.SetTags(transaction.Tags.Where(x => x.Id != tagId));
        }
    }

    public void Add(Tag tag)
    {
        _dbContext.Tags.Add(tag);
    }

    public void Delete(Tag tag)
    {
        _dbContext.Tags.Remove(tag);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
