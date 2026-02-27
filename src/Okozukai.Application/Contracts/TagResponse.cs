using Okozukai.Domain.Transactions;

namespace Okozukai.Application.Contracts;

public sealed record TagResponse(Guid Id, string Name, string Color)
{
    public static TagResponse FromDomain(Tag tag) => new(tag.Id, tag.Name, tag.Color);
}
