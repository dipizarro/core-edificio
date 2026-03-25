using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

/// <summary>
/// Repositorio encargado de gestionar bloqueos administrativos y mantenciones sobre las instalaciones (Facilities).
/// </summary>
public class FacilityBlockRepository : IFacilityBlockRepository
{
    private readonly AppDbContext _db;

    public FacilityBlockRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Guarda un nuevo bloqueo u hora de mantención inhabilitando su uso.
    /// </summary>
    public async Task AddAsync(FacilityBlock block, CancellationToken ct = default)
    {
        await _db.FacilityBlocks.AddAsync(block, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Obtiene un bloque de instalación particular (Tracking activo para posibilitar modificaciones).
    /// </summary>
    public Task<FacilityBlock?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.FacilityBlocks.FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>
    /// Obtiene la lista de todos los bloqueos activos que intersecten con un rango de fechas.
    /// </summary>
    public Task<List<FacilityBlock>> GetActiveBlocksInRangeAsync(Guid facilityId, DateTime start, DateTime end, CancellationToken ct = default)
        => _db.FacilityBlocks
            .Where(x => x.FacilityId == facilityId && x.IsActive && x.StartAtUtc < end && x.EndAtUtc > start)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(ct);

    /// <summary>
    /// Actualiza el estado de un bloqueo (e.g. Desactivarlo o extenderlo).
    /// </summary>
    public async Task UpdateAsync(FacilityBlock block, CancellationToken ct = default)
    {
        _db.FacilityBlocks.Update(block);
        await _db.SaveChangesAsync(ct);
    }
}
