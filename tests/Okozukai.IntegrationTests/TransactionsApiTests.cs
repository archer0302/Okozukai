using System.Net;
using System.Net.Http.Json;
using Okozukai.Application.Contracts;
using Okozukai.Domain.Transactions;

namespace Okozukai.IntegrationTests;

public class TransactionsApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TransactionsApiTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<JournalResponse> CreateDefaultJournalAsync(string currency = "USD")
    {
        var response = await _client.PostAsJsonAsync("/api/journals", new CreateJournalRequest("Test Journal", currency));
        response.EnsureSuccessStatusCode();
        var journal = await response.Content.ReadFromJsonAsync<JournalResponse>();
        Assert.NotNull(journal);
        return journal;
    }

    [Fact]
    public async Task CreateTransaction_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var request = new CreateTransactionRequest(
            journal.Id,
            TransactionType.In,
            100.50m,
            DateTimeOffset.UtcNow,
            "Initial deposit");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(transaction);
        Assert.Equal(request.Amount, transaction.Amount);
        Assert.Equal(journal.Id, transaction.JournalId);
        Assert.Equal("USD", transaction.Currency);
    }

    [Fact]
    public async Task GetAllTransactions_ReturnsOk()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();

        // Act
        var response = await _client.GetAsync($"/api/transactions?journalId={journal.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transactions = await response.Content.ReadFromJsonAsync<IEnumerable<TransactionResponse>>();
        Assert.NotNull(transactions);
    }

    [Fact]
    public async Task GetAllTransactions_WithPaging_ReturnsPagedResult()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.In, 100m, DateTimeOffset.UtcNow, "A"));
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 20m, DateTimeOffset.UtcNow.AddMinutes(1), "B"));

        // Act
        var response = await _client.GetAsync($"/api/transactions?journalId={journal.Id}&page=1&pageSize=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transactions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<TransactionResponse>>();
        Assert.NotNull(transactions);
        Assert.Single(transactions);
    }

    [Fact]
    public async Task GetSummary_ReturnsBalance()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync("CAD");
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.In, 100m, DateTimeOffset.UtcNow, "Income"));
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 40m, DateTimeOffset.UtcNow.AddMinutes(1), "Expense"));

        // Act
        var response = await _client.GetAsync($"/api/transactions/summary?journalId={journal.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<TransactionSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal("CAD", summary.Currency);
        Assert.Equal(100m, summary.TotalIn);
        Assert.Equal(40m, summary.TotalOut);
        Assert.Equal(60m, summary.Net);
    }

    [Fact]
    public async Task GetAllTransactions_WithInvalidPaging_UsesSafeDefaults()
    {
        var journal = await CreateDefaultJournalAsync();
        var response = await _client.GetAsync($"/api/transactions?journalId={journal.Id}&page=0&pageSize=500");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTransactions_WithInvalidDateRange_ReturnsBadRequest()
    {
        var journal = await CreateDefaultJournalAsync();
        var response = await _client.GetAsync($"/api/transactions?journalId={journal.Id}&from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGrouped_ReturnsYearMonthGroupsWithRollups()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.In, 100m, new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero), "Income"));
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 30m, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero), "Expense"));
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 50m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), "Jan expense"));

        var response = await _client.GetAsync($"/api/transactions/grouped?journalId={journal.Id}&from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var grouped = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<TransactionYearGroupResponse>>();
        Assert.NotNull(grouped);
        var year = grouped.Single(x => x.Year == 2026);
        Assert.Equal([2, 1], year.Months.Select(x => x.Month).ToArray());

        var feb = year.Months.Single(x => x.Month == 2).Rollups.Single();
        Assert.Equal(100m, feb.TotalIn);
        Assert.Equal(30m, feb.TotalOut);
        Assert.Equal(70m, feb.NetChange);

        var jan = year.Months.Single(x => x.Month == 1).Rollups.Single();
        Assert.Equal(50m, jan.TotalOut);
    }

    [Fact]
    public async Task GetSpendingByTag_ReturnsTagTotals()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Food"));
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(tag);

        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 90m, DateTimeOffset.UtcNow, "Dinner", TagIds: [tag.Id]));

        var response = await _client.GetAsync($"/api/transactions/spending-by-tag?journalId={journal.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var spending = await response.Content.ReadFromJsonAsync<TransactionSpendingByTagResponse>();
        Assert.NotNull(spending);

        var food = spending.Items.Single(x => x.TagId == tag.Id);
        Assert.Equal(90m, food.TotalOut);
    }

    [Fact]
    public async Task GetSpendingByTagMonthly_GroupsByMonthAndTag()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("MonthlyTransit"));
        tagResponse.EnsureSuccessStatusCode();
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(tag);

        // Two Out transactions in different months
        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero);
        var janResp = await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 40m, jan, "Bus Jan", TagIds: [tag.Id]));
        janResp.EnsureSuccessStatusCode();
        var febResp = await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 60m, feb, "Bus Feb", TagIds: [tag.Id]));
        febResp.EnsureSuccessStatusCode();
        // Income should be excluded
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.In, 200m, jan, "Salary"));

        var response = await _client.GetAsync($"/api/transactions/spending-by-tag-monthly?journalId={journal.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SpendingByTagMonthlyResponse>();
        Assert.NotNull(result);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(2, result.Months.Count);

        var janMonth = result.Months.Single(m => m.Year == 2024 && m.Month == 1);
        Assert.Single(janMonth.Items);
        var janItem = janMonth.Items.Single();
        Assert.Equal(tag.Id, janItem.TagId);
        Assert.Equal(40m, janItem.TotalOut);

        var febMonth = result.Months.Single(m => m.Year == 2024 && m.Month == 2);
        Assert.Single(febMonth.Items);
        Assert.Equal(60m, febMonth.Items.Single().TotalOut);
    }

    [Fact]
    public async Task GetSpendingByTagMonthly_WithEmptyJournalId_ReturnsEmptyMonths()
    {
        // Guid.Empty is the default when no journalId query param is provided;
        // no journal matches, so we get OK with an empty months list.
        var response = await _client.GetAsync("/api/transactions/spending-by-tag-monthly?journalId=00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SpendingByTagMonthlyResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Months);
    }
    [Fact]
    public async Task Export_WithCsvFormat_ReturnsCsvFile()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 12m, DateTimeOffset.UtcNow, "Snack"));

        var response = await _client.GetAsync($"/api/transactions/export?journalId={journal.Id}&format=csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Id,JournalId", content);
        Assert.Contains("Snack", content);
    }

    [Fact]
    public async Task Export_WithUnsupportedFormat_ReturnsBadRequest()
    {
        var journal = await CreateDefaultJournalAsync();
        var response = await _client.GetAsync($"/api/transactions/export?journalId={journal.Id}&format=xml");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TagsCrud_Works()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Groceries"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var tag = await createResponse.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(tag);

        var listResponse = await _client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var tags = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<TagResponse>>();
        Assert.NotNull(tags);
        Assert.Contains(tags, t => t.Id == tag.Id);

        var updateResponse = await _client.PutAsJsonAsync($"/api/tags/{tag.Id}", new UpdateTagRequest("Dining"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    [Fact]
    public async Task GetAllTransactions_CanFilterByTagIds()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var tagResponse = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Transport"));
        var tag = await tagResponse.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(tag);

        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 15m, DateTimeOffset.UtcNow, "Bus", TagIds: [tag.Id]));
        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 30m, DateTimeOffset.UtcNow, "Other", null));

        var response = await _client.GetAsync($"/api/transactions?journalId={journal.Id}&tagIds={tag.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var filtered = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<TransactionResponse>>();
        Assert.NotNull(filtered);
        Assert.All(filtered, t => Assert.Contains(t.Tags, x => x.Id == tag.Id));
    }

    [Fact]
    public async Task DeleteTag_DetachesFromLinkedTransactions_AndDeletesTag()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var createTagResponse = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Bills"));
        var tag = await createTagResponse.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(tag);

        await _client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(journal.Id, TransactionType.Out, 45m, DateTimeOffset.UtcNow, "Phone", TagIds: [tag.Id]));

        var deleteResponse = await _client.DeleteAsync($"/api/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listTagsResponse = await _client.GetAsync("/api/tags");
        var tags = await listTagsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<TagResponse>>();
        Assert.NotNull(tags);
        Assert.DoesNotContain(tags, t => t.Id == tag.Id);

        var transactionsResponse = await _client.GetAsync($"/api/transactions?journalId={journal.Id}");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<TransactionResponse>>();
        Assert.NotNull(transactions);
        Assert.All(transactions, t => Assert.DoesNotContain(t.Tags, x => x.Id == tag.Id));
    }

    [Fact]
    public async Task CreateTag_WithDuplicateName_ReturnsConflict()
    {
        var first = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Leisure"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Leisure"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/transactions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        var request = new CreateTransactionRequest(journal.Id, TransactionType.In, -10m, DateTimeOffset.UtcNow, null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JournalsCrud_Works()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/journals", new CreateJournalRequest("My Journal", "EUR"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var journal = await createResponse.Content.ReadFromJsonAsync<JournalResponse>();
        Assert.NotNull(journal);
        Assert.Equal("My Journal", journal.Name);
        Assert.Equal("EUR", journal.PrimaryCurrency);
        Assert.False(journal.IsClosed);

        // Get all
        var listResponse = await _client.GetAsync("/api/journals");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var journals = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<JournalResponse>>();
        Assert.NotNull(journals);
        Assert.Contains(journals, j => j.Id == journal.Id);

        // Update
        var updateResponse = await _client.PutAsJsonAsync($"/api/journals/{journal.Id}", new UpdateJournalRequest("Renamed"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JournalResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated.Name);

        // Close
        var closeResponse = await _client.PostAsync($"/api/journals/{journal.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JournalResponse>();
        Assert.NotNull(closed);
        Assert.True(closed.IsClosed);

        // Reopen
        var reopenResponse = await _client.PostAsync($"/api/journals/{journal.Id}/reopen", null);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var reopened = await reopenResponse.Content.ReadFromJsonAsync<JournalResponse>();
        Assert.NotNull(reopened);
        Assert.False(reopened.IsClosed);
    }

    [Fact]
    public async Task DeleteJournal_WhenOpen_ReturnsConflict()
    {
        var journal = await CreateDefaultJournalAsync();
        var response = await _client.DeleteAsync($"/api/journals/{journal.Id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteJournal_WhenClosed_ReturnsNoContent()
    {
        var journal = await CreateDefaultJournalAsync();
        await _client.PostAsync($"/api/journals/{journal.Id}/close", null);

        var response = await _client.DeleteAsync($"/api/journals/{journal.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_InClosedJournal_ReturnsConflict()
    {
        // Arrange
        var journal = await CreateDefaultJournalAsync();
        await _client.PostAsync($"/api/journals/{journal.Id}/close", null);

        var request = new CreateTransactionRequest(journal.Id, TransactionType.In, 100m, DateTimeOffset.UtcNow, "Should fail");

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
