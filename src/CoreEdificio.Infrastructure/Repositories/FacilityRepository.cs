using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

/// <summary>
/// Repositorio concreto para gestionar Instalaciones utilizando Entity Framework Core.
/// </summary>
public class FacilityRepository : IFacilityRepository
{
    private readonly AppDbContext _db;

    public FacilityRepository(AppDbContext db) => _db = db;

    public Task<Facility?> GetByIdAsync(Guid communityId, Guid facilityId, CancellationToken ct = default)
        => _db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId && f.CommunityId == communityId, ct);

    public Task<List<Facility>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _db.Facilities
            .Where(f => f.CommunityId == communityId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Facility facility, CancellationToken ct = default)
    {
        _db.Facilities.Add(facility);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Facility facility, CancellationToken ct = default)
    {
        _db.Facilities.Update(facility);
        await _db.SaveChangesAsync(ct);
    }
}
