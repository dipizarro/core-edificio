using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories;

/// <summary>
/// Provee acceso a datos sobre Unidades y Componentes asociados (bodegas, estacionamientos)
/// optimizando las transacciones por agrupación (Bulk).
/// </summary>
public class UnitRepository : IUnitRepository
{
    private readonly AppDbContext _db;
    
    public UnitRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Verifica si una comunidad existe mediante su Guid de forma rápida (Any).
    /// </summary>
    public Task<bool> CommunityExistsAsync(Guid communityId, CancellationToken ct = default)
        => _db.Communities.AnyAsync(x => x.Id == communityId, ct);

    /// <summary>
    /// Verifica si el número de unidad (departamento) ya ha sido registrado dentro de una misma comunidad.
    /// </summary>
    public Task<bool> UnitNumberExistsAsync(Guid communityId, string number, CancellationToken ct = default)
        => _db.Units.AnyAsync(x => x.CommunityId == communityId && x.Number == number, ct);

    /// <summary>
    /// Agrega una única unidad y la asegura de manera asíncrona en el contexto EF.
    /// </summary>
    public async Task AddAsync(Unit unit, CancellationToken ct = default)
    {
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Realiza una inserción múltiple de unidades minimizando la latencia transaccional de Base de Datos.
    /// </summary>
    public async Task AddRangeAsync(List<Unit> units, CancellationToken ct = default)
    {
        _db.Units.AddRange(units);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Identifica cuáles unidades ya existen a partir de un listado entrante por número, devolviendo un HashSet ultra rápido.
    /// </summary>
    public async Task<HashSet<string>> GetExistingUnitNumbersAsync(Guid communityId, IEnumerable<string> numbers, CancellationToken ct = default)
    {
        var existing = await _db.Units
            .Where(x => x.CommunityId == communityId && numbers.Contains(x.Number))
            .Select(x => x.Number)
            .ToListAsync(ct);
        return existing.ToHashSet();
    }

    /// <summary>
    /// Devuelve todas las unidades de una comunidad en formato de lectura rápida (AsNoTracking).
    /// </summary>
    public Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default)
        => _db.Units.AsNoTracking()
            .Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.Number)
            .ToListAsync(ct);

    /// <summary>
    /// Obtiene eficientemente una cuenta global y suma total de los coeficientes de participación sin hidratar objetos a memoria.
    /// </summary>
    public async Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default)
    {
        var count = await _db.Units.CountAsync(x => x.CommunityId == communityId, ct);
        var total = await _db.Units.Where(x => x.CommunityId == communityId).SumAsync(x => x.CoefficientPct, ct);
        return (count, total);
    }

    /// <summary>
    /// Determina la existencia de componentes específicos (e.g., Tipo+Codigo => Estacionamiento|5A) globalmente en la comunidad.
    /// </summary>
    public async Task<HashSet<string>> GetExistingComponentKeysAsync(Guid communityId, IEnumerable<string> keys, CancellationToken ct = default)
    {
        var existing = await _db.UnitComponents
            .Where(x => x.CommunityId == communityId)
            .Select(x => x.Type + "|" + x.Code)
            .Where(k => keys.Contains(k))
            .ToListAsync(ct);

        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
