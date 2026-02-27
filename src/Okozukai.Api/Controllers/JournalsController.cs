using Microsoft.AspNetCore.Mvc;
using Okozukai.Application.Contracts;
using Okozukai.Application.Transactions;

namespace Okozukai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JournalsController : ControllerBase
{
    private readonly JournalService _journalService;

    public JournalsController(JournalService journalService)
    {
        _journalService = journalService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<JournalResponse>>> GetAll(CancellationToken ct)
    {
        var journals = await _journalService.GetAllAsync(ct);
        return Ok(journals);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JournalResponse>> GetById(Guid id, CancellationToken ct)
    {
        var journal = await _journalService.GetByIdAsync(id, ct);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpPost]
    public async Task<ActionResult<JournalResponse>> Create(CreateJournalRequest request, CancellationToken ct)
    {
        var journal = await _journalService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = journal.Id }, journal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JournalResponse>> Update(Guid id, UpdateJournalRequest request, CancellationToken ct)
    {
        var journal = await _journalService.UpdateAsync(id, request, ct);
        return journal is null ? NotFound() : Ok(journal);
    }

    /// <summary>
    /// Deletes a journal. Only closed journals can be deleted; all their transactions are cascade-deleted.
    /// Returns 409 Conflict if the journal is not closed.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await _journalService.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<JournalResponse>> Close(Guid id, CancellationToken ct)
    {
        var journal = await _journalService.CloseAsync(id, ct);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<JournalResponse>> Reopen(Guid id, CancellationToken ct)
    {
        var journal = await _journalService.ReopenAsync(id, ct);
        return journal is null ? NotFound() : Ok(journal);
    }
}
