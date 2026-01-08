using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class UnitChargeRepository : IUnitChargeRepository
{
    private readonly AppDbContext _db;
    public UnitChargeRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(List<UnitCharge> charges, CancellationToken ct = default)
    {
        _db.UnitCharges.AddRange(charges);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> AnyByBillingPeriodIdAsync(Guid billingPeriodId, CancellationToken ct = default)
    => _db.UnitCharges.AsNoTracking().AnyAsync(x => x.BillingPeriodId == billingPeriodId, ct);

    public Task<List<UnitCharge>> GetChargesBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.UnitCharges.AsNoTracking()
            .Include(x => x.BillingPeriod)
            .Where(x => x.UnitId == unitId && string.Compare(x.BillingPeriod.Period, period) < 0)
            .ToListAsync(ct);

    public Task<List<UnitCharge>> GetChargesForPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.UnitCharges.AsNoTracking()
            .Include(x => x.BillingPeriod)
            .Where(x => x.UnitId == unitId && x.BillingPeriod.Period == period)
            .ToListAsync(ct);
}
