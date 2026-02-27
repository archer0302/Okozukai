using Microsoft.Extensions.Logging;
using Okozukai.Application.Contracts;
using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Transactions;

public sealed class TagService
{
    private static readonly string[] ColorPalette =
    [
        "#6366f1", // indigo
        "#f97316", // orange
        "#10b981", // emerald
        "#ec4899", // pink
        "#3b82f6", // blue
        "#f59e0b", // amber
        "#14b8a6", // teal
        "#a855f7", // purple
        "#ef4444", // red
        "#84cc16", // lime
        "#06b6d4", // cyan
        "#f472b6", // fuchsia-pink
    ];

    private readonly ITagRepository _repository;
    private readonly ILogger<TagService> _logger;

    public TagService(ITagRepository repository, ILogger<TagService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TagResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var tags = await _repository.GetAllAsync(ct);
        return tags.Select(TagResponse.FromDomain).ToArray();
    }

    public async Task<TagResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _repository.GetByIdAsync(id, ct);
        return tag is null ? null : TagResponse.FromDomain(tag);
    }

    public async Task<TagResponse> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        var normalizedName = request.Name.Trim();
        var existing = await _repository.GetByNameAsync(normalizedName, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Tag '{normalizedName}' already exists.");
        }

        var allTags = await _repository.GetAllAsync(ct);
        var color = ColorPalette[allTags.Count % ColorPalette.Length];

        var tag = Tag.Create(request.Name, color);
        _repository.Add(tag);
        await _repository.SaveChangesAsync(ct);
        _logger.LogInformation("Created tag {TagId} ({TagName}) with color {Color}.", tag.Id, tag.Name, color);
        return TagResponse.FromDomain(tag);
    }

    public async Task<TagResponse?> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tag = await _repository.GetByIdAsync(id, ct);
        if (tag is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();
        var existing = await _repository.GetByNameAsync(normalizedName, ct);
        if (existing is not null && existing.Id != id)
        {
            throw new InvalidOperationException($"Tag '{normalizedName}' already exists.");
        }

        tag.Rename(request.Name);
        await _repository.SaveChangesAsync(ct);
        _logger.LogInformation("Updated tag {TagId}.", id);
        return TagResponse.FromDomain(tag);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _repository.GetByIdAsync(id, ct);
        if (tag is null)
        {
            return false;
        }

        await _repository.DetachFromTransactionsAsync(id, ct);
        _repository.Delete(tag);
        await _repository.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted tag {TagId}.", id);
        return true;
    }
}
