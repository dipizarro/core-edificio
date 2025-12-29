using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

public interface ICommunityRepository
{
    Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Community>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Community community, CancellationToken ct = default);
}
