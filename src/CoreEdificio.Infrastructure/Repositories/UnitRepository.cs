using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

public class UnitRepository : IUnitRepository
{
    private readonly AppDbContext _db;
    public UnitRepository(AppDbContext db) => _db = db;

    public Task<bool> CommunityExistsAsync(Guid communityId, CancellationToken ct = default)
        => _db.Communities.AnyAsync(x => x.Id == communityId, ct);

    public Task<bool> UnitNumberExistsAsync(Guid communityId, string number, CancellationToken ct = default)
        => _db.Units.AnyAsync(x => x.CommunityId == communityId && x.Number == number, ct);

    public async Task AddAsync(Unit unit, CancellationToken ct = default)
    {
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _db.Units.AsNoTracking()
            .Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.Number)
            .ToListAsync(ct);

    public async Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default)
    {
        var count = await _db.Units.CountAsync(x => x.CommunityId == communityId, ct);
        var total = await _db.Units.Where(x => x.CommunityId == communityId).SumAsync(x => x.CoefficientPct, ct);
        return (count, total);
    }
}
