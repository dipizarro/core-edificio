using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

/// <summary>
/// Repositorio de la infraestructura principal para la gestión de entidades de tipo Comunidad (Community).
/// Implementa operaciones asíncronas optimizadas y seguras usando Entity Framework Core.
/// </summary>
public class CommunityRepository : ICommunityRepository
{
    private readonly AppDbContext _db;

    public CommunityRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Obtiene una comunidad mediante su identificador único (ID) sin seguimiento asNoTracking para lectura rápida.
    /// </summary>
    public Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Communities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>
    /// Lista todas las comunidades ordenadas de forma descendente por fecha de creación (solo lectura).
    /// </summary>
    public Task<List<Community>> ListAsync(CancellationToken ct = default)
        => _db.Communities.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    /// <summary>
    /// Persiste una nueva entidad de Comunidad en la base de datos de manera transaccional.
    /// </summary>
    public async Task AddAsync(Community community, CancellationToken ct = default)
    {
        _db.Communities.Add(community);
        await _db.SaveChangesAsync(ct);
    }
}
