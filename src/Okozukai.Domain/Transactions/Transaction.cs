namespace Okozukai.Domain.Transactions;

public sealed class Transaction
{
    public Guid Id { get; }
    public Guid JournalId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public string? Note { get; private set; }
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();
    private readonly List<Tag> _tags = [];

    // Navigation property populated by EF Core.
    public Journal? Journal { get; private set; }

    // Used by EF Core for entity materialization — bypasses domain validation.
#pragma warning disable CS8618
    private Transaction() { }
#pragma warning restore CS8618

    private Transaction(
        Guid id,
        Guid journalId,
        TransactionType type,
        decimal amount,
        DateTimeOffset occurredAt,
        string? note,
        DateTimeOffset createdAt)
    {
        Id = id;
        JournalId = journalId;
        Type = type;
        Amount = ValidateAmount(amount);
        OccurredAt = occurredAt;
        Note = NormalizeNote(note);
        CreatedAt = createdAt;
    }

    public static Transaction Create(
        Guid journalId,
        bool journalIsClosed,
        TransactionType type,
        decimal amount,
        DateTimeOffset occurredAt,
        string? note)
    {
        if (journalIsClosed)
        {
            throw new InvalidOperationException("Cannot add a transaction to a closed journal.");
        }

        return new Transaction(Guid.NewGuid(), journalId, type, amount, occurredAt, note, DateTimeOffset.UtcNow);
    }

    public void Update(
        bool journalIsClosed,
        TransactionType type,
        decimal amount,
        DateTimeOffset occurredAt,
        string? note)
    {
        if (journalIsClosed)
        {
            throw new InvalidOperationException("Cannot modify a transaction in a closed journal.");
        }

        Type = type;
        Amount = ValidateAmount(amount);
        OccurredAt = occurredAt;
        Note = NormalizeNote(note);
    }

    public void SetTags(IEnumerable<Tag> tags)
    {
        _tags.Clear();
        _tags.AddRange(tags.DistinctBy(x => x.Id));
    }

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        return amount;
    }

    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
