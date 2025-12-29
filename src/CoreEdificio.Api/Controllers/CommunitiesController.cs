using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[ApiController]
[Route("api/communities")]
public class CommunitiesController : ControllerBase
{
    private readonly CommunityService _service;
    public CommunitiesController(CommunityService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(CreateCommunityCommand cmd, CancellationToken ct)
    {
        var created = await _service.CreateAsync(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var c = await _service.GetByIdAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));
}
