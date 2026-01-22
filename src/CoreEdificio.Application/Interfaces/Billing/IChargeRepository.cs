using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IChargeRepository
{
    Task AddAsync(Charge charge, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<Charge> charges, CancellationToken ct);
    Task<bool> ExistsAsync(string sourceType, Guid sourceId, string chargeKind, CancellationToken ct);
    Task<List<Charge>> GetForUnitAndPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct);
    Task<List<Charge>> GetBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct);
}
