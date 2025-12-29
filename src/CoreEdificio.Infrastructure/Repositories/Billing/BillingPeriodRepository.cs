using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class BillingPeriodRepository : IBillingPeriodRepository
{
    private readonly AppDbContext _db;
    public BillingPeriodRepository(AppDbContext db) => _db = db;

    public Task<BillingPeriod?> GetByCommunityAndPeriodAsync(Guid communityId, string period, CancellationToken ct = default)
        => _db.BillingPeriods.FirstOrDefaultAsync(x => x.CommunityId == communityId && x.Period == period, ct);

    public async Task AddAsync(BillingPeriod billingPeriod, CancellationToken ct = default)
    {
        _db.BillingPeriods.Add(billingPeriod);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BillingPeriod billingPeriod, CancellationToken ct = default)
    {
        _db.BillingPeriods.Update(billingPeriod);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BillingSummaryDto?> GetSummaryAsync(Guid communityId, string period, CancellationToken ct = default)
    {
        var billing = await _db.BillingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommunityId == communityId && x.Period == period, ct);

        if (billing is null) return null;

        var charges = await (
            from ch in _db.UnitCharges.AsNoTracking()
            join u in _db.Units.AsNoTracking() on ch.UnitId equals u.Id
            where ch.BillingPeriodId == billing.Id
            orderby u.Number
            select new UnitChargeDto(
                u.Id,
                u.Number,
                ch.CoefficientPct,
                ch.Amount
            )
        ).ToListAsync(ct);

        return new BillingSummaryDto(
            billing.CommunityId,
            billing.Period,
            billing.TotalExpenses,
            billing.TotalCoefficientPct,
            charges.Count,
            charges
        );
    }
}
