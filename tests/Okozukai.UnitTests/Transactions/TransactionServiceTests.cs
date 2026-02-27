using Microsoft.Extensions.Logging;
using Moq;
using Okozukai.Application.Contracts;
using Okozukai.Application.Transactions;
using Okozukai.Domain.Transactions;

namespace Okozukai.UnitTests.Transactions;

public sealed class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _repositoryMock;
    private readonly Mock<IJournalRepository> _journalRepositoryMock;
    private readonly Mock<ITagRepository> _tagRepositoryMock;
    private readonly Mock<ILogger<TransactionService>> _loggerMock;
    private readonly TransactionService _sut;

    private static readonly Guid TestJournalId = Guid.NewGuid();
    private static readonly Journal TestJournal = Journal.Create("Test Journal", "USD");

    public TransactionServiceTests()
    {
        _repositoryMock = new Mock<ITransactionRepository>();
        _journalRepositoryMock = new Mock<IJournalRepository>();
        _tagRepositoryMock = new Mock<ITagRepository>();
        _loggerMock = new Mock<ILogger<TransactionService>>();
        _sut = new TransactionService(
            _repositoryMock.Object,
            _journalRepositoryMock.Object,
            _tagRepositoryMock.Object,
            _loggerMock.Object);

        // Default: journal exists and is open
        _journalRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestJournal);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsResponseAndSaves()
    {
        // Arrange
        var request = new CreateTransactionRequest(
            TestJournal.Id,
            TransactionType.In,
            100.50m,
            DateTimeOffset.UtcNow,
            "Initial deposit");

        var createdTransaction = Transaction.Create(TestJournal.Id, false, request.Type, request.Amount, request.OccurredAt, request.Note);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTransaction);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Amount, result.Amount);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Once);
        _repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateAsync_WhenJournalIsClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var closedJournal = Journal.Create("Closed", "USD");
        closedJournal.Close();
        _journalRepositoryMock
            .Setup(x => x.GetByIdAsync(closedJournal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedJournal);

        var request = new CreateTransactionRequest(closedJournal.Id, TransactionType.In, 100m, DateTimeOffset.UtcNow, null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(request));
        _repositoryMock.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenJournalNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _journalRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Journal?)null);

        var request = new CreateTransactionRequest(Guid.NewGuid(), TransactionType.In, 100m, DateTimeOffset.UtcNow, null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsResponse()
    {
        // Arrange
        var transaction = Transaction.Create(TestJournal.Id, false, TransactionType.Out, 50m, DateTimeOffset.UtcNow, null);
        _repositoryMock.Setup(x => x.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.GetByIdAsync(transaction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transaction.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WhenFound_UpdatesAndReturnsResponse()
    {
        // Arrange
        var transaction = Transaction.Create(TestJournal.Id, false, TransactionType.In, 100m, DateTimeOffset.UtcNow, null);
        _repositoryMock.Setup(x => x.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var request = new UpdateTransactionRequest(TransactionType.Out, 200m, DateTimeOffset.UtcNow.AddDays(1), "Updated");

        // Act
        var result = await _sut.UpdateAsync(transaction.Id, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Amount, result.Amount);
        _repositoryMock.Verify(x => x.Update(It.IsAny<Transaction>()), Times.Once);
        _repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteAsync_WhenFound_DeletesAndReturnsTrue()
    {
        // Arrange
        var transaction = Transaction.Create(TestJournal.Id, false, TransactionType.In, 100m, DateTimeOffset.UtcNow, null);
        _repositoryMock.Setup(x => x.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _journalRepositoryMock
            .Setup(x => x.GetByIdAsync(transaction.JournalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestJournal);

        // Act
        var result = await _sut.DeleteAsync(transaction.Id);

        // Assert
        Assert.True(result);
        _repositoryMock.Verify(x => x.Delete(It.IsAny<Transaction>()), Times.Once);
        _repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenJournalClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var closedJournal = Journal.Create("Closed", "USD");
        closedJournal.Close();
        var transaction = Transaction.Create(closedJournal.Id, false, TransactionType.In, 100m, DateTimeOffset.UtcNow, null);
        _repositoryMock.Setup(x => x.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _journalRepositoryMock
            .Setup(x => x.GetByIdAsync(transaction.JournalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedJournal);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(transaction.Id));
        _repositoryMock.Verify(x => x.Delete(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_WithPaging_ReturnsExpectedPage()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var transactions = new[]
        {
            Transaction.Create(TestJournal.Id, false, TransactionType.In, 100m, now.AddMinutes(-2), "A"),
            Transaction.Create(TestJournal.Id, false, TransactionType.Out, 20m, now.AddMinutes(-1), "B"),
        };
        _repositoryMock.Setup(x => x.GetPagedAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _sut.GetAllAsync(TestJournal.Id, page: 1, pageSize: 2);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsSingleCurrencyBalance()
    {
        // Arrange
        var summaries = new[] { new TransactionCurrencySummary("USD", 100m, 10m) };
        _repositoryMock.Setup(x => x.GetSummaryAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        // Act
        var result = await _sut.GetSummaryAsync(TestJournal.Id);

        // Assert
        Assert.Equal("USD", result.Currency);
        Assert.Equal(90m, result.Net);
    }

    [Fact]
    public async Task GetSpendingByTagMonthlyAsync_GroupsByMonthAndTag()
    {
        // Arrange
        var tag = Tag.Create("Food", "#10b981");
        var jan = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero);

        var t1 = Transaction.Create(TestJournal.Id, false, TransactionType.Out, 50m, jan, "Grocery");
        t1.SetTags(new[] { tag });
        var t2 = Transaction.Create(TestJournal.Id, false, TransactionType.Out, 30m, jan, "Restaurant");
        t2.SetTags(new[] { tag });
        var t3 = Transaction.Create(TestJournal.Id, false, TransactionType.Out, 20m, feb, "Snack");
        // t3 has no tags → "Untagged"
        var t4 = Transaction.Create(TestJournal.Id, false, TransactionType.In, 500m, jan, "Salary");
        // Income should be excluded

        _repositoryMock.Setup(x => x.GetForGroupingAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { t1, t2, t3, t4 });

        // Act
        var result = await _sut.GetSpendingByTagMonthlyAsync(TestJournal.Id);

        // Assert
        Assert.Equal("USD", result.Currency);
        Assert.Equal(2, result.Months.Count);

        var janMonth = result.Months.First(m => m.Month == 1);
        Assert.Equal(2026, janMonth.Year);
        Assert.Single(janMonth.Items);
        Assert.Equal("Food", janMonth.Items.First().TagName);
        Assert.Equal(80m, janMonth.Items.First().TotalOut);

        var febMonth = result.Months.First(m => m.Month == 2);
        Assert.Single(febMonth.Items);
        Assert.Equal("Untagged", febMonth.Items.First().TagName);
        Assert.Equal(20m, febMonth.Items.First().TotalOut);
    }

    [Fact]
    public async Task GetSpendingByTagMonthlyAsync_ExcludesIncomeTransactions()
    {
        // Arrange
        var jan = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var income = Transaction.Create(TestJournal.Id, false, TransactionType.In, 1000m, jan, "Salary");

        _repositoryMock.Setup(x => x.GetForGroupingAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { income });

        // Act
        var result = await _sut.GetSpendingByTagMonthlyAsync(TestJournal.Id);

        // Assert
        Assert.Empty(result.Months);
    }
}
