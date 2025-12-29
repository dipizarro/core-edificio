using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class UnitReadRepository : IUnitReadRepository
{
    private readonly AppDbContext _db;
    public UnitReadRepository(AppDbContext db) => _db = db;

    public Task<List<UnitSnapshot>> ListSnapshotsByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _db.Units.AsNoTracking()
            .Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.Number)
            .Select(x => new UnitSnapshot(x.Id, x.Number, x.CoefficientPct))
            .ToListAsync(ct);
}
