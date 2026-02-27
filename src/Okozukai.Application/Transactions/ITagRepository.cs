using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public interface ITagRepository
{
    Task<IReadOnlyCollection<Tag>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<Tag>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tag?> GetByNameAsync(string normalizedName, CancellationToken ct = default);
    Task DetachFromTransactionsAsync(Guid tagId, CancellationToken ct = default);
    void Add(Tag tag);
    void Delete(Tag tag);
    Task SaveChangesAsync(CancellationToken ct = default);
}
