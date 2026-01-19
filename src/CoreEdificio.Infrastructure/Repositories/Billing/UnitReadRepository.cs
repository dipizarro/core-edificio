using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class UnitReadRepository : IUnitReadRepository
{
    private readonly AppDbContext _db;
    public UnitReadRepository(AppDbContext db) => _db = db;

    public async Task<List<UnitSnapshot>> ListSnapshotsByCommunityAsync(Guid communityId, CancellationToken ct = default)
    {
        var units = await _db.Units.AsNoTracking()
            .Include(x => x.Components)
            .Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.Number)
            .ToListAsync(ct);

        return units.Select(x => new UnitSnapshot(
            x.Id, 
            x.Number, 
            x.GetTotalCoefficientPct(),
            x.Components.Select(c => new UnitComponentSnapshot(c.Type, c.Code, c.CoefficientPct, c.IsActive)).ToList()
        )).ToList();
    }
}
