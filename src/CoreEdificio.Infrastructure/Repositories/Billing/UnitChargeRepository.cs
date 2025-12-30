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

}
