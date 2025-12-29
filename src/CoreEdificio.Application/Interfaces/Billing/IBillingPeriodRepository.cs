using CoreEdificio.Application.Contracts.Billing;
using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IBillingPeriodRepository
{
    Task<BillingPeriod?> GetByCommunityAndPeriodAsync(Guid communityId, string period, CancellationToken ct = default);
    Task AddAsync(BillingPeriod billingPeriod, CancellationToken ct = default);
    Task UpdateAsync(BillingPeriod billingPeriod, CancellationToken ct = default);
    Task<BillingSummaryDto?> GetSummaryAsync(Guid communityId, string period, CancellationToken ct = default);
}
