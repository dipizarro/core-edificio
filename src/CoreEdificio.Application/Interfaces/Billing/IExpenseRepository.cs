using CoreEdificio.Domain.Entities.Billing;

namespace CoreEdificio.Application.Interfaces.Billing;

public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken ct = default);
    Task AddRangeAsync(List<Expense> expenses, CancellationToken ct = default);
    Task<decimal> GetTotalByCommunityAndPeriodAsync(Guid communityId, string period, CancellationToken ct = default);
    Task<bool> AnyForIssuedPeriodAsync(Guid communityId, string period, CancellationToken ct = default);
}
