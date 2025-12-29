using CoreEdificio.Application.Common;
using CoreEdificio.Application.Contracts;
using CoreEdificio.Application.Interfaces;
using CoreEdificio.Domain.Entities;

namespace CoreEdificio.Application.Services;

public class CommunityService
{
    private readonly ICommunityRepository _repo;

    public CommunityService(ICommunityRepository repo) => _repo = repo;

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

    public Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(id, ct);

    public Task<List<Community>> ListAsync(CancellationToken ct = default)
        => _repo.ListAsync(ct);
}
