using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts.Payments;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[Authorize(Policy = AuthPolicies.CommunityScope)]
[ApiController]
[Route("api/communities/{communityId:guid}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsService _payments;

    public PaymentsController(PaymentsService payments) => _payments = payments;

    // Registrar pago
    // POST /api/communities/{communityId}/payments
    [HttpPost]
    public async Task<IActionResult> Register(Guid communityId, RegisterPaymentCommand cmd, CancellationToken ct)
    {
        var created = await _payments.RegisterAsync(communityId, cmd, ct);

        return Created(
            $"/api/communities/{communityId}/payments/{created.Id}",
            created
        );
    }

    // Listar pagos
    // GET /api/communities/{communityId}/payments?unitId=...&period=YYYY-MM
    [HttpGet]
    public async Task<IActionResult> List(Guid communityId, [FromQuery] Guid? unitId, [FromQuery] string? period, CancellationToken ct)
    {
        var items = await _payments.ListAsync(communityId, unitId, period, ct);
        return Ok(items);
    }
}
