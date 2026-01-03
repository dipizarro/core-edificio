using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers;

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

}
