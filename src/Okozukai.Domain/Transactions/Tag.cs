namespace Okozukai.Domain.Transactions;

public sealed class Tag
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Color { get; private set; }

    private Tag()
    {
        Name = string.Empty;
        Color = string.Empty;
    }

    private Tag(Guid id, string name, string color)
    {
        Id = id;
        Name = NormalizeName(name);
        Color = color;
    }

    public static Tag Create(string name, string color)
    {
        return new Tag(Guid.NewGuid(), name, color);
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        var normalized = name.Trim();
        if (normalized.Length > 60)
        {
            throw new ArgumentException("Tag name must be 60 characters or fewer.", nameof(name));
        }

        return normalized;
    }
}
