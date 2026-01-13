using CoreEdificio.Application.Contracts.Residents;

namespace CoreEdificio.Application.Interfaces.Identity;

public interface IIdentityService
{
    Task<List<ResidentWithUnitsDto>> GetResidentsWithUnitsAsync(Guid communityId, CancellationToken ct = default);
}
