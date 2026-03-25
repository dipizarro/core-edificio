using CoreEdificio.Api.Auth;
using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Contracts.Billing.Bulk;
using CoreEdificio.Application.Services;
using CoreEdificio.Application.Interfaces.Billing;
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
    private readonly IStatementPdfGenerator _pdfGenerator;

    public BillingController(BillingService billing, PaymentsService payments, IStatementPdfGenerator pdfGenerator)
    {
        _billing = billing;
        _payments = payments;
        _pdfGenerator = pdfGenerator;
    }

    /// <summary>
    /// Registra un nuevo gasto común individual para un periodo específico.
    /// </summary>
    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(Guid communityId, CreateExpenseCommand cmd, CancellationToken ct)
    {
        var created = await _billing.CreateExpenseAsync(communityId, cmd, ct);
        return Created($"/api/communities/{communityId}/billing/expenses/{created.Id}", created);
    }

    /// <summary>
    /// Registra múltiples gastos comunes de forma masiva (Bulk) para facilitar la digitación.
    /// </summary>
    [HttpPost("expenses/bulk")]
    public async Task<IActionResult> CreateExpensesBulk(Guid communityId, CreateExpensesBulkCommand cmd, CancellationToken ct)
    {
        var response = await _billing.CreateExpensesBulkAsync(communityId, cmd, ct);
        return Ok(response);
    }

    /// <summary>
    /// Emite oficialmente un periodo contable, realizando el prorrateo automático y bloqueando modificaciones futuras.
    /// </summary>
    [HttpPost("{period}/issue")]
    public async Task<IActionResult> Issue(Guid communityId, string period, CancellationToken ct)
    {
        var summary = await _billing.IssueAsync(communityId, new IssueBillingPeriodCommand(period), ct);
        return Ok(summary);
    }

    /// <summary>
    /// Obtiene el resumen de facturación y prorrateo de un periodo específico.
    /// </summary>
    [HttpGet("{period}")]
    public async Task<IActionResult> GetSummary(Guid communityId, string period, CancellationToken ct)
    {
        var summary = await _billing.GetSummaryAsync(communityId, period, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Obtiene el listado de unidades con pagos incompletos o en morosidad (Arrears) para el periodo.
    /// </summary>
    [HttpGet("{period}/arrears")]
    public async Task<IActionResult> GetArrears(Guid communityId, string period, CancellationToken ct)
    {
        var items = await _payments.GetArrearsAsync(communityId, period, ct);
        return Ok(items);
    }

    /// <summary>
    /// Consulta el estado de cuenta y cobros detallados de una unidad en particular (Vista de Administración).
    /// </summary>
    [HttpGet("units/{unitId:guid}/statement/{period}")]
    public async Task<IActionResult> GetStatement(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        return Ok(statement);
    }

    /// <summary>
    /// Consulta el estado de cuenta propio del Residente autenticado.
    /// </summary>
    [HttpGet("my-statement/{period}")]
    [Authorize(Roles = "Resident")] 
    public async Task<IActionResult> GetMyStatement(Guid communityId, string period, CancellationToken ct)
    {
        var unitIdClaim = User.FindFirst("UnitId")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !Guid.TryParse(unitIdClaim, out var unitId))
        {
             return Forbid();
        }

        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        return Ok(statement);
    }

    /// <summary>
    /// Descarga el documento PDF del estado de cuenta de una unidad específica.
    /// </summary>
    [HttpGet("units/{unitId:guid}/statement/{period}/pdf")]
    public async Task<IActionResult> GetStatementPdf(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        var pdfBytes = await _pdfGenerator.GenerateUnitStatementPdfAsync(statement, ct);
        var filename = $"CoreEdificio_Statement_{statement.UnitNumber}_{period}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    /// <summary>
    /// Descarga el documento PDF del estado de cuenta del Residente autenticado.
    /// </summary>
    [HttpGet("my-statement/{period}/pdf")]
    [Authorize(Roles = "Resident")]
    public async Task<IActionResult> GetMyStatementPdf(Guid communityId, string period, CancellationToken ct)
    {
        var unitIdClaim = User.FindFirst("UnitId")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !Guid.TryParse(unitIdClaim, out var unitId))
        {
            return Forbid();
        }

        var statement = await _billing.GetUnitStatementAsync(communityId, unitId, period, ct);
        var pdfBytes = await _pdfGenerator.GenerateUnitStatementPdfAsync(statement, ct);
        var filename = $"CoreEdificio_Statement_{statement.UnitNumber}_{period}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }
}
