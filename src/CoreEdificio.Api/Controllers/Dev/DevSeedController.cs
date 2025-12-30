using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Services;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CoreEdificio.Api.Controllers.Dev;

[ApiController]
[Route("api/dev/seed")]
public class DevSeedController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly BillingService _billing;

    public DevSeedController(IWebHostEnvironment env, AppDbContext db, BillingService billing)
    {
        _env = env;
        _db = db;
        _billing = billing;
    }

    [HttpPost("billing")]
    public async Task<IActionResult> SeedBilling([FromQuery] string period = "2026-01", CancellationToken ct = default)
    {
        if (!_env.IsDevelopment())
            return NotFound(); // no existe fuera de Dev

        // 1) Community
        var community = new Community
        {
            Name = "Comunidad Demo CoreEdificio",
            Address = "Av. Demo 123, Santiago"
        };
        _db.Communities.Add(community);

        // 2) Units (coef sum 100)
        var units = new[]
        {
            ("101", 8.50m), ("102", 9.00m), ("103", 10.25m), ("104", 11.00m),
            ("201", 8.25m), ("202", 9.75m), ("203", 10.00m), ("204", 11.25m),
            ("301", 10.00m), ("302", 12.00m)
        }.Select(u => new Unit
        {
            CommunityId = community.Id,
            Number = u.Item1,
            CoefficientPct = u.Item2
        }).ToList();

        _db.Units.AddRange(units);

        await _db.SaveChangesAsync(ct);

        // 3) Expenses (realistas)
        var expenses = new[]
        {
            ("Conserjería", 2400000m),
            ("Aseo", 650000m),
            ("Electricidad áreas comunes", 320450m),
            ("Agua áreas comunes", 180200m),
            ("Mantención ascensores", 280000m),
            ("Bombas / sala bombas", 120000m),
            ("CCTV / monitoreo", 90000m),
            ("Basura / reciclaje", 65000m),
            ("Seguro edificio", 210000m),
            ("Gastos bancarios / admin", 25000m),
        };

        foreach (var e in expenses)
            await _billing.CreateExpenseAsync(community.Id, new CreateExpenseCommand(period, e.Item1, e.Item2), ct);

        // 4) Issue
        var summary = await _billing.IssueAsync(community.Id, new IssueBillingPeriodCommand(period), ct);

        return Ok(new
        {
            communityId = community.Id,
            period,
            totalExpenses = summary.TotalExpenses,
            unitsCount = summary.UnitsCount,
            chargesTotal = summary.Charges.Sum(x => x.Amount),
            summary
        });
    }
}
