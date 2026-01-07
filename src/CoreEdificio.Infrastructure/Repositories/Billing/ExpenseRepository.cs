using CoreEdificio.Application.Interfaces.Billing;
using CoreEdificio.Domain.Entities.Billing;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Billing;

public class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;
    public ExpenseRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
    {
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(List<Expense> expenses, CancellationToken ct = default)
    {
        _db.Expenses.AddRange(expenses);
        await _db.SaveChangesAsync(ct);
    }

    public Task<decimal> GetTotalByCommunityAndPeriodAsync(Guid communityId, string period, CancellationToken ct = default)
        => _db.Expenses
            .Where(x => x.CommunityId == communityId && x.Period == period)
            .SumAsync(x => x.Amount, ct);

    public Task<bool> AnyForIssuedPeriodAsync(Guid communityId, string period, CancellationToken ct = default)
        => _db.BillingPeriods.AnyAsync(x => x.CommunityId == communityId && x.Period == period && x.Status == BillingPeriodStatus.Issued, ct);
}
