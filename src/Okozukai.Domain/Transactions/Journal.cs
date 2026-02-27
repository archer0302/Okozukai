namespace Okozukai.Domain.Transactions;

public sealed class Journal
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string PrimaryCurrency { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    // Used by EF Core for entity materialization — bypasses domain validation.
#pragma warning disable CS8618
    private Journal() { }
#pragma warning restore CS8618

    private Journal(Guid id, string name, string primaryCurrency, DateTimeOffset createdAt)
    {
        Id = id;
        Name = ValidateName(name);
        PrimaryCurrency = NormalizeCurrency(primaryCurrency);
        IsClosed = false;
        CreatedAt = createdAt;
    }

    public static Journal Create(string name, string primaryCurrency)
    {
        return new Journal(Guid.NewGuid(), name, primaryCurrency, DateTimeOffset.UtcNow);
    }

    public void Update(string name)
    {
        Name = ValidateName(name);
    }

    public void Close()
    {
        IsClosed = true;
    }

    public void Reopen()
    {
        IsClosed = false;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Journal name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 100)
        {
            throw new ArgumentException("Journal name must be 100 characters or fewer.", nameof(name));
        }

        return trimmed;
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Primary currency is required.", nameof(currency));
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new ArgumentException("Primary currency must be a 3-letter ISO code.", nameof(currency));
        }

        return normalized;
    }
}
