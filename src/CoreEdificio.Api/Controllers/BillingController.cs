using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Contracts.Billing.Bulk;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

[Authorize(Roles = "Committee,Admin")]
[Authorize(Policy = AuthPolicies.CommunityScope)]
[ApiController]
[Route("api/communities/{communityId:guid}/billing")]
public class BillingController : ControllerBase
{
    private readonly BillingService _billing;
    private readonly PaymentsService _payments;
    public BillingController(BillingService billing, PaymentsService payments)
    {
        _billing = billing;
        _payments = payments;
    }


    // Crear gasto
    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(Guid communityId, CreateExpenseCommand cmd, CancellationToken ct)
    {
        var created = await _billing.CreateExpenseAsync(communityId, cmd, ct);
        return Created($"/api/communities/{communityId}/billing/expenses/{created.Id}", created);
    }

    [HttpPost("expenses/bulk")]
    public async Task<IActionResult> CreateExpensesBulk(Guid communityId, CreateExpensesBulkCommand cmd, CancellationToken ct)
    {
        var response = await _billing.CreateExpensesBulkAsync(communityId, cmd, ct);
        return Ok(response);
    }

    // Emitir período
    [HttpPost("{period}/issue")]
    public async Task<IActionResult> Issue(Guid communityId, string period, CancellationToken ct)
    {
        var summary = await _billing.IssueAsync(communityId, new IssueBillingPeriodCommand(period), ct);
        return Ok(summary);
    }

    // Obtener resumen (desde DB)
    [HttpGet("{period}")]
    public async Task<IActionResult> GetSummary(Guid communityId, string period, CancellationToken ct)
    {
        var summary = await _billing.GetSummaryAsync(communityId, period, ct);
        return Ok(summary);
    }

    // GET /api/communities/{communityId}/billing/{period}/arrears
    [HttpGet("{period}/arrears")]
    public async Task<IActionResult> GetArrears(Guid communityId, string period, CancellationToken ct)
    {
        var items = await _payments.GetArrearsAsync(communityId, period, ct);
        return Ok(items);
    }

    /// <summary>
    /// Consulta estado de cuenta de una unidad (Admin/Committee)
    /// </summary>
    [HttpGet("units/{unitId:guid}/statement/{period}")]
    public async Task<IActionResult> GetStatement(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        return Ok(statement);
    }

    /// <summary>
    /// Consulta MI estado de cuenta (Resident)
    /// </summary>
    [HttpGet("my-statement/{period}")]
    [Authorize(Roles = "Resident")] 
    // AuthPolicies.CommunityScope ya está a nivel de controlador, pero necesitamos UnitId del claim
    public async Task<IActionResult> GetMyStatement(Guid communityId, string period, CancellationToken ct)
    {
        // Obtener UnitId de los claims
        var unitIdClaim = User.FindFirst("UnitId")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !Guid.TryParse(unitIdClaim, out var unitId))
        {
             return Forbid(); // O 400 Bad Request si preferimos
        }

        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        return Ok(statement);
    }

}
