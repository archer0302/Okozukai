using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public interface IJournalRepository
{
    Task<IReadOnlyCollection<Journal>> GetAllAsync(CancellationToken ct = default);
    Task<Journal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Journal journal);
    void Update(Journal journal);
    void Delete(Journal journal);
    Task SaveChangesAsync(CancellationToken ct = default);
}
