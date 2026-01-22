using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

public interface IFacilityBlockRepository
{
    Task AddAsync(FacilityBlock block, CancellationToken ct = default);
    Task<FacilityBlock?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<FacilityBlock>> GetActiveBlocksInRangeAsync(Guid facilityId, DateTime start, DateTime end, CancellationToken ct = default);
    Task UpdateAsync(FacilityBlock block, CancellationToken ct = default);
}
