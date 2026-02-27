using Okozukai.Domain.Transactions;

namespace Okozukai.UnitTests.Transactions;

public sealed class TransactionTests
{
    private static readonly Guid TestJournalId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsTransaction()
    {
        // Arrange
        var type = TransactionType.Out;
        var amount = 100.50m;
        var occurredAt = DateTimeOffset.UtcNow;
        var note = "Test transaction";

        // Act
        var transaction = Transaction.Create(TestJournalId, journalIsClosed: false, type, amount, occurredAt, note);

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(TestJournalId, transaction.JournalId);
        Assert.Equal(type, transaction.Type);
        Assert.Equal(amount, transaction.Amount);
        Assert.Equal(occurredAt, transaction.OccurredAt);
        Assert.Equal(note, transaction.Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidAmount_ThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.Create(TestJournalId, journalIsClosed: false, TransactionType.Out, invalidAmount, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Create_WhenJournalIsClosed_ThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            Transaction.Create(TestJournalId, journalIsClosed: true, TransactionType.In, 100m, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        // Arrange
        var transaction = Transaction.Create(TestJournalId, journalIsClosed: false, TransactionType.In, 100m, DateTimeOffset.UtcNow, "Old");
        var newType = TransactionType.Out;
        var newAmount = 200m;
        var newOccurredAt = DateTimeOffset.UtcNow.AddHours(1);
        var newNote = "New";

        // Act
        transaction.Update(journalIsClosed: false, newType, newAmount, newOccurredAt, newNote);

        // Assert
        Assert.Equal(newType, transaction.Type);
        Assert.Equal(newAmount, transaction.Amount);
        Assert.Equal(newOccurredAt, transaction.OccurredAt);
        Assert.Equal(newNote, transaction.Note);
    }

    [Fact]
    public void Update_WhenJournalIsClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var transaction = Transaction.Create(TestJournalId, journalIsClosed: false, TransactionType.In, 100m, DateTimeOffset.UtcNow, null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            transaction.Update(journalIsClosed: true, TransactionType.Out, 200m, DateTimeOffset.UtcNow, null));
    }
}
