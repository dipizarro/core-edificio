using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[Authorize(Policy = AuthPolicies.CommunityScope)]
[ApiController]
public class UnitsController : ControllerBase
{
    private readonly UnitService _service;
    public UnitsController(UnitService service) => _service = service;

    [HttpPost("api/communities/{communityId:guid}/units")]
    public async Task<IActionResult> Create(Guid communityId, CreateUnitCommand cmd, CancellationToken ct)
    {
        var created = await _service.CreateAsync(communityId, cmd, ct);
        return Created($"/api/units/{created.Id}", created);
    }

    [HttpGet("api/communities/{communityId:guid}/units")]
    public async Task<IActionResult> ListByCommunity(Guid communityId, CancellationToken ct)
        => Ok(await _service.ListByCommunityAsync(communityId, ct));

    [HttpGet("api/communities/{communityId:guid}/units/coefficients-summary")]
    public async Task<IActionResult> CoeffSummary(Guid communityId, CancellationToken ct)
    {
        var (count, total) = await _service.GetCoefficientSummaryAsync(communityId, ct);
        return Ok(new { communityId, unitsCount = count, totalCoefficientPct = total });
    }
}
