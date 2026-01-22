using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class ChargeRepository : IChargeRepository
{
    private readonly AppDbContext _db;

    public ChargeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Charge charge, CancellationToken ct)
    {
        await _db.Charges.AddAsync(charge, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Charge> charges, CancellationToken ct)
    {
        await _db.Charges.AddRangeAsync(charges, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string sourceType, Guid sourceId, string chargeKind, CancellationToken ct)
    {
        return await _db.Charges.AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId && x.ChargeKind == chargeKind, ct);
    }

    public async Task<List<Charge>> GetForUnitAndPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        return await _db.Charges
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && x.Period == period)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Charge>> GetBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct)
    {
        return await _db.Charges
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && string.Compare(x.Period, period) < 0)
            .ToListAsync(ct);
    }
}
