using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Bulk;
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

    [HttpPost("api/communities/{communityId:guid}/units/bulk")]
    public async Task<IActionResult> CreateBulk(Guid communityId, CreateUnitsBulkCommand cmd, CancellationToken ct)
    {
        var response = await _service.CreateBulkAsync(communityId, cmd, ct);
        return Ok(response);
    }

    [HttpPost("api/communities/{communityId:guid}/units/bulk-with-components")]
    public async Task<IActionResult> CreateBulkWithComponents(
        Guid communityId, 
        CoreEdificio.Api.Contracts.CreateUnitsWithComponentsBulkRequest request, 
        CancellationToken ct)
    {
        var command = new CreateUnitsWithComponentsBulkCommand(
            communityId,
            request.Units.Select(u => new CreateUnitWithComponentsDto(
                u.UnitNumber,
                u.Components.Select(c => new CreateUnitComponentDto(c.Type, c.Code, c.CoefficientPct)).ToList()
            )).ToList()
        );

        var response = await _service.CreateBulkWithComponentsAsync(communityId, command, ct);
        return Ok(response);
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
