using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

/// <summary>
/// Provee abstracción para el acceso a datos de las Instalaciones (Facilities).
/// </summary>
public interface IFacilityRepository
{
    Task<Facility?> GetByIdAsync(Guid communityId, Guid facilityId, CancellationToken ct = default);
    Task<List<Facility>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default);
    Task AddAsync(Facility facility, CancellationToken ct = default);
    Task UpdateAsync(Facility facility, CancellationToken ct = default);
}
