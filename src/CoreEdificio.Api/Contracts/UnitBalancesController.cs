using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[ApiController]
[Route("api/communities/{communityId:guid}/units/{unitId:guid}/balance")]
public class UnitBalancesController : ControllerBase
{
    private readonly PaymentsService _payments;

    public UnitBalancesController(PaymentsService payments) => _payments = payments;

    // GET /api/communities/{communityId}/units/{unitId}/balance/{period}
    [HttpGet("{period}")]
    public async Task<IActionResult> Get(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        var dto = await _payments.GetBalanceAsync(communityId, unitId, period, ct);
        return Ok(dto);
    }
}
