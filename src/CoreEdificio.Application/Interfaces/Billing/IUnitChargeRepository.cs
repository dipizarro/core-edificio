using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IUnitChargeRepository
{
    Task AddRangeAsync(List<UnitCharge> charges, CancellationToken ct = default);
    Task<bool> AnyByBillingPeriodIdAsync(Guid billingPeriodId, CancellationToken ct = default);

}
