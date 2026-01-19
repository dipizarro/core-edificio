namespace CoreEdificio.Application.Interfaces.Billing;

public record UnitSnapshot(Guid UnitId, string Number, decimal CoefficientPct, List<UnitComponentSnapshot> Components);
public record UnitComponentSnapshot(string Type, string Code, decimal CoefficientPct, bool IsActive);

public interface IUnitReadRepository
{
    Task<List<UnitSnapshot>> ListSnapshotsByCommunityAsync(Guid communityId, CancellationToken ct = default);
}
