using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

public class CommunityRepository : ICommunityRepository
{
    private readonly AppDbContext _db;
    public CommunityRepository(AppDbContext db) => _db = db;

    public Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Communities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Community>> ListAsync(CancellationToken ct = default)
        => _db.Communities.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    public async Task AddAsync(Community community, CancellationToken ct = default)
    {
        _db.Communities.Add(community);
        await _db.SaveChangesAsync(ct);
    }
}
