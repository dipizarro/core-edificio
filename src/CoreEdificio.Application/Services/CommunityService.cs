using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Contracts.Residents;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Application.Interfaces.Identity;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

public class CommunityService
{
    private readonly ICommunityRepository _repo;
    private readonly IIdentityService _identity;

    public CommunityService(ICommunityRepository repo, IIdentityService identity)
    {
        _repo = repo;
        _identity = identity;
    }

    public async Task<Community> CreateAsync(CreateCommunityCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new ValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(cmd.Address)) throw new ValidationException("Address is required.");

        var community = new Community
        {
            Name = cmd.Name.Trim(),
            Address = cmd.Address.Trim()
        };

        await _repo.AddAsync(community, ct);
        return community;
    }

    public async Task<List<ResidentWithUnitsDto>> GetResidentsWithUnitsAsync(Guid communityId, CancellationToken ct = default)
    {
        return await _identity.GetResidentsWithUnitsAsync(communityId, ct);
    }

    public Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(id, ct);

    public Task<List<Community>> ListAsync(CancellationToken ct = default)
        => _repo.ListAsync(ct);
}
