namespace CoreEdificio.Application.Interfaces.Billing;

public record UnitSnapshot(Guid UnitId, string Number, decimal CoefficientPct);

public interface IUnitReadRepository
{
    Task<List<UnitSnapshot>> ListSnapshotsByCommunityAsync(Guid communityId, CancellationToken ct = default);
}
