using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Payments;

public class BillingReadRepository : IBillingReadRepository
{
    private readonly AppDbContext _db;
    public BillingReadRepository(AppDbContext db) => _db = db;

    public Task<bool> IsPeriodIssuedAsync(Guid communityId, string period, CancellationToken ct = default)
        => _db.BillingPeriods.AsNoTracking()
            .AnyAsync(x => x.CommunityId == communityId && x.Period == period && x.Status == BillingPeriodStatus.Issued, ct);

    public async Task<decimal?> GetUnitChargeAmountAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
    {
        var billing = await _db.BillingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommunityId == communityId && x.Period == period, ct);

        if (billing is null) return null;

        var charge = await _db.UnitCharges.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BillingPeriodId == billing.Id && x.UnitId == unitId, ct);

        return charge?.Amount;
    }

    public async Task<List<(Guid UnitId, string UnitNumber, decimal ChargeAmount)>> ListUnitChargesAsync(
    Guid communityId,
    string period,
    CancellationToken ct = default)
    {
        var billing = await _db.BillingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommunityId == communityId && x.Period == period, ct);

        if (billing is null) return [];

        var list = await (
            from ch in _db.UnitCharges.AsNoTracking()
            join u in _db.Units.AsNoTracking() on ch.UnitId equals u.Id
            where ch.BillingPeriodId == billing.Id
            select new
            {
                ch.UnitId,
                UnitNumber = u.Number,
                ch.Amount
            }
        ).ToListAsync(ct);

        return list.Select(x => (x.UnitId, x.UnitNumber, x.Amount)).ToList();
    }

}
