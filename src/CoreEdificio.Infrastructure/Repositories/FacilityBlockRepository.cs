using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

public class FacilityBlockRepository : IFacilityBlockRepository
{
    private readonly AppDbContext _db;

    public FacilityBlockRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(FacilityBlock block, CancellationToken ct = default)
    {
        await _db.FacilityBlocks.AddAsync(block, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<FacilityBlock?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.FacilityBlocks.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<List<FacilityBlock>> GetActiveBlocksInRangeAsync(Guid facilityId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        return _db.FacilityBlocks
            .Where(x => x.FacilityId == facilityId && x.IsActive && x.StartAtUtc < end && x.EndAtUtc > start)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(FacilityBlock block, CancellationToken ct = default)
    {
        _db.FacilityBlocks.Update(block);
        await _db.SaveChangesAsync(ct);
    }
}
