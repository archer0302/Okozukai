using Microsoft.AspNetCore.Mvc;
using Okozukai.Application.Contracts;
using Okozukai.Application.Transactions;

namespace Okozukai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TransactionResponse>>> GetAll(
        [FromQuery] Guid journalId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid[]? tagIds,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? noteSearch = null,
        CancellationToken ct = default)
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["from"] = ["The 'from' date must be earlier than or equal to 'to'."]
            }));
        }

        var transactions = await _transactionService.GetAllAsync(journalId, from, to, tagIds, page, pageSize, noteSearch, ct);
        return Ok(transactions);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TransactionSummaryResponse>> GetSummary(
        [FromQuery] Guid journalId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid[]? tagIds,
        [FromQuery] string? noteSearch = null,
        CancellationToken ct = default)
    {
        var summary = await _transactionService.GetSummaryAsync(journalId, from, to, tagIds, noteSearch, ct);
        return Ok(summary);
    }

    [HttpGet("grouped")]
    public async Task<ActionResult<IReadOnlyCollection<TransactionYearGroupResponse>>> GetGrouped(
        [FromQuery] Guid journalId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid[]? tagIds,
        [FromQuery] string? noteSearch = null,
        CancellationToken ct = default)
    {
        var grouped = await _transactionService.GetGroupedAsync(journalId, from, to, tagIds, noteSearch, ct);
        return Ok(grouped);
    }

    [HttpGet("spending-by-tag")]
    public async Task<ActionResult<TransactionSpendingByTagResponse>> GetSpendingByTag(
        [FromQuery] Guid journalId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid[]? tagIds,
        [FromQuery] string? noteSearch = null,
        CancellationToken ct = default)
    {
        var spending = await _transactionService.GetSpendingByTagAsync(journalId, from, to, tagIds, noteSearch, ct);
        return Ok(spending);
    }

    [HttpGet("spending-by-tag-monthly")]
    public async Task<ActionResult<SpendingByTagMonthlyResponse>> GetSpendingByTagMonthly(
        [FromQuery] Guid journalId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid[]? tagIds,
        [FromQuery] string? noteSearch = null,
        CancellationToken ct = default)
    {
        var spending = await _transactionService.GetSpendingByTagMonthlyAsync(journalId, from, to, tagIds, noteSearch, ct);
        return Ok(spending);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid journalId,
        [FromQuery] string format = "json",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] Guid[]? tagIds = null,
        CancellationToken ct = default)
    {
        var export = await _transactionService.ExportAsync(journalId, format, from, to, tagIds, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> GetById(Guid id, CancellationToken ct)
    {
        var transaction = await _transactionService.GetByIdAsync(id, ct);
        if (transaction is null)
        {
            return NotFound();
        }

        return Ok(transaction);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(CreateTransactionRequest request, CancellationToken ct)
    {
        var transaction = await _transactionService.CreateAsync(request, ct);
        return CreatedAtAction(
            nameof(GetById),
            new { id = transaction.Id },
            transaction);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(Guid id, UpdateTransactionRequest request, CancellationToken ct)
    {
        var transaction = await _transactionService.UpdateAsync(id, request, ct);
        if (transaction is null)
        {
            return NotFound();
        }

        return Ok(transaction);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await _transactionService.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
