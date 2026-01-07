using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Interfaces;

public interface IUnitRepository
{
    Task<bool> CommunityExistsAsync(Guid communityId, CancellationToken ct = default);
    Task<bool> UnitNumberExistsAsync(Guid communityId, string number, CancellationToken ct = default);
    Task<HashSet<string>> GetExistingUnitNumbersAsync(Guid communityId, IEnumerable<string> numbers, CancellationToken ct = default);

    Task AddAsync(Unit unit, CancellationToken ct = default);
    Task AddRangeAsync(List<Unit> units, CancellationToken ct = default);
    Task<List<Unit>> ListByCommunityAsync(Guid communityId, CancellationToken ct = default);
    Task<(int Count, decimal TotalCoefficientPct)> GetCoefficientSummaryAsync(Guid communityId, CancellationToken ct = default);
}
