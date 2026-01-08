using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IUnitChargeRepository
{
    Task AddRangeAsync(List<UnitCharge> charges, CancellationToken ct = default);
    Task<bool> AnyByBillingPeriodIdAsync(Guid billingPeriodId, CancellationToken ct = default);

    Task<List<UnitCharge>> GetChargesBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default);
    Task<List<UnitCharge>> GetChargesForPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default);

}
