using Okozukai.Domain.Transactions;

namespace Okozukai.UnitTests.Transactions;

public sealed class JournalTests
{
    [Fact]
    public void Create_WithValidData_ReturnsJournal()
    {
        var journal = Journal.Create("My Budget", "JPY");

        Assert.NotEqual(Guid.Empty, journal.Id);
        Assert.Equal("My Budget", journal.Name);
        Assert.Equal("JPY", journal.PrimaryCurrency);
        Assert.False(journal.IsClosed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => Journal.Create(name!, "USD"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData(null)]
    public void Create_WithInvalidCurrency_ThrowsArgumentException(string? currency)
    {
        Assert.Throws<ArgumentException>(() => Journal.Create("Name", currency!));
    }

    [Fact]
    public void Create_NormalizesUppercaseCurrency()
    {
        var journal = Journal.Create("Name", "usd");
        Assert.Equal("USD", journal.PrimaryCurrency);
    }

    [Fact]
    public void Close_SetsIsClosedTrue()
    {
        var journal = Journal.Create("Name", "USD");
        journal.Close();
        Assert.True(journal.IsClosed);
    }

    [Fact]
    public void Reopen_SetsIsClosedFalse()
    {
        var journal = Journal.Create("Name", "USD");
        journal.Close();
        journal.Reopen();
        Assert.False(journal.IsClosed);
    }

    [Fact]
    public void Update_ChangesName()
    {
        var journal = Journal.Create("Old Name", "USD");
        journal.Update("New Name");
        Assert.Equal("New Name", journal.Name);
    }

    [Fact]
    public void Create_WithNameExceeding100Chars_ThrowsArgumentException()
    {
        var longName = new string('A', 101);
        Assert.Throws<ArgumentException>(() => Journal.Create(longName, "USD"));
    }
}
