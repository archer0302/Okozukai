using Microsoft.AspNetCore.Mvc;
using Okozukai.Application.Contracts;
using Okozukai.Application.Transactions;

namespace Okozukai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TagsController : ControllerBase
{
    private readonly TagService _tagService;

    public TagsController(TagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TagResponse>>> GetAll(CancellationToken ct)
    {
        var tags = await _tagService.GetAllAsync(ct);
        return Ok(tags);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagResponse>> GetById(Guid id, CancellationToken ct)
    {
        var tag = await _tagService.GetByIdAsync(id, ct);
        return tag is null ? NotFound() : Ok(tag);
    }

    [HttpPost]
    public async Task<ActionResult<TagResponse>> Create(CreateTagRequest request, CancellationToken ct)
    {
        var tag = await _tagService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagResponse>> Update(Guid id, UpdateTagRequest request, CancellationToken ct)
    {
        var tag = await _tagService.UpdateAsync(id, request, ct);
        return tag is null ? NotFound() : Ok(tag);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await _tagService.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
