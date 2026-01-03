using CoreEdificio.Domain.Entities.Payments;

namespace CoreEdificio.Application.Interfaces.Payments;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken ct = default);

    Task<decimal> GetPaidTotalAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default);

    Task<List<Payment>> ListAsync(Guid communityId, Guid? unitId, string? period, CancellationToken ct = default);
}
