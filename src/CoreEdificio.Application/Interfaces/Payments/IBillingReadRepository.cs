namespace CoreEdificio.Application.Interfaces.Payments;

public interface IBillingReadRepository
{
    Task<bool> IsPeriodIssuedAsync(Guid communityId, string period, CancellationToken ct = default);

    Task<decimal?> GetUnitChargeAmountAsync(Guid communityId, Guid unitId, string period, CancellationToken ct = default);

    Task<List<(Guid UnitId, string UnitNumber, decimal ChargeAmount)>> ListUnitChargesAsync(Guid communityId, string period, CancellationToken ct = default);
}
