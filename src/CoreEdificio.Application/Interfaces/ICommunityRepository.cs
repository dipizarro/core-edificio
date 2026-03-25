using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

/// <summary>
/// Contrato para el acceso a datos de la entidad Community.
/// </summary>
public interface ICommunityRepository
{
    /// <summary>
    /// Obtiene una comunidad por su identificador.
    /// </summary>
    Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// Lista todas las comunidades registradas.
    /// </summary>
    Task<List<Community>> ListAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Agrega una nueva comunidad al repositorio.
    /// </summary>
    Task AddAsync(Community community, CancellationToken ct = default);
}
