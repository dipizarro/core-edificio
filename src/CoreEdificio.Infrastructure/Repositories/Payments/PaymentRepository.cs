using CoreEdificio.Application.Interfaces.Payments;
using CoreEdificio.Domain.Entities.Payments;
using CoreEdificio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreEdificio.Infrastructure.Repositories.Payments;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;
    public PaymentRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
    }

    public Task<decimal> GetPaidTotalAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && x.Period == period)
            .SumAsync(x => x.Amount, ct);

    public async Task<List<Payment>> ListAsync(Guid communityId, Guid? unitId, string? period, CancellationToken ct = default)
    {
        var q = _db.Payments.AsNoTracking().Where(x => x.CommunityId == communityId);

        if (unitId.HasValue) q = q.Where(x => x.UnitId == unitId.Value);
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(x => x.Period == period);

        return await q.OrderByDescending(x => x.PaidAtUtc).ToListAsync(ct);
    }

    public Task<List<Payment>> GetPaymentsBeforePeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && string.Compare(x.Period, period) < 0)
            .ToListAsync(ct);

    public Task<List<Payment>> GetPaymentsForPeriodAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default)
        => _db.Payments.AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.UnitId == unitId && x.Period == period)
            .ToListAsync(ct);
    
}
